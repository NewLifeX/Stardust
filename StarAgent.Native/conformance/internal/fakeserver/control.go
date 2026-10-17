package fakeserver

import (
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"strings"
	"time"

	"staragent-conformance/internal/protocol"
)

type controlCommandRequest struct {
	Command *protocol.CommandModel `json:"Command"`
	Via     string                 `json:"Via"`
}

type pingControlRequest struct {
	Period      *int                    `json:"Period"`
	NewServer   *string                 `json:"NewServer"`
	SyncTime    *int64                  `json:"SyncTime"`
	Commands    []protocol.CommandModel `json:"Commands"`
	RotateToken *bool                   `json:"RotateToken"`
}

type eventsFailRequest struct {
	Count  int `json:"Count"`
	Status int `json:"Status"`
}

type wsControlRequest struct {
	Disabled *bool `json:"Disabled"`
}

type upgradeControlRequest struct {
	Clear bool `json:"Clear"`
	protocol.UpgradeInfo
}

func (s *Server) handleControl(w http.ResponseWriter, r *http.Request, path string) {
	switch path {
	case "/control/state":
		writeJSON(w, http.StatusOK, s.State())
	case "/control/command":
		s.controlCommand(w, r)
	case "/control/ping":
		s.controlPing(w, r)
	case "/control/kick":
		s.controlKick(w, r)
	case "/control/events-fail":
		s.controlEventsFail(w, r)
	case "/control/ws":
		s.controlWS(w, r)
	case "/control/upgrade":
		s.controlUpgrade(w, r)
	default:
		http.Error(w, "unknown control endpoint", http.StatusNotFound)
	}
}

func (s *Server) controlCommand(w http.ResponseWriter, r *http.Request) {
	body, err := io.ReadAll(io.LimitReader(r.Body, 1<<20))
	if err != nil {
		writeError(w, http.StatusBadRequest, err.Error())

		return
	}
	via := strings.ToLower(strings.TrimSpace(r.URL.Query().Get("via")))

	var cmd protocol.CommandModel
	var wrapper controlCommandRequest
	if err := json.Unmarshal(body, &wrapper); err == nil && wrapper.Command != nil {
		cmd = *wrapper.Command
		if wrapper.Via != "" {
			via = strings.ToLower(wrapper.Via)
		}
	} else if err := json.Unmarshal(body, &cmd); err != nil {
		writeError(w, http.StatusBadRequest, "invalid command body: "+err.Error())

		return
	}

	if cmd.Id == 0 || cmd.Command == "" {
		writeError(w, http.StatusBadRequest, "Command.Id and Command.Command are required")

		return
	}
	if via == "" {
		via = "auto"
	}
	if err := s.enqueueCommand(cmd, via); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())

		return
	}
	writeJSON(w, http.StatusOK, map[string]any{"ok": true, "via": via})
}

func (s *Server) enqueueCommand(cmd protocol.CommandModel, via string) error {
	s.mu.Lock()
	disabled := s.disableWS
	active := len(s.conns) > 0
	s.mu.Unlock()

	if via == "auto" {
		if disabled || !active {
			via = "http"
		} else {
			via = "ws"
		}
	}

	switch via {
	case "http":
		s.mu.Lock()
		s.httpCommands = append(s.httpCommands, cmd)
		s.deliveries = append(s.deliveries, DeliveryRecord{
			At:     time.Now().UTC(),
			Id:     cmd.Id,
			Via:    "http",
			Status: "queued",
		})
		s.mu.Unlock()

		return nil
	case "ws":
		s.mu.Lock()
		var target *wsConn
		for _, c := range s.conns {
			target = c
			break
		}
		if target == nil {
			s.wsPending = append(s.wsPending, cmd)
			s.deliveries = append(s.deliveries, DeliveryRecord{
				At:     time.Now().UTC(),
				Id:     cmd.Id,
				Via:    "ws",
				Status: "queued",
			})
			s.mu.Unlock()

			return nil
		}
		s.mu.Unlock()

		rec := DeliveryRecord{At: time.Now().UTC(), Id: cmd.Id, Via: "ws", Status: "sent"}
		if err := s.sendCommand(target, cmd); err != nil {
			rec.Status = "error"
			rec.Error = err.Error()
			s.mu.Lock()
			s.wsPending = append(s.wsPending, cmd)
			s.deliveries = append(s.deliveries, rec)
			s.mu.Unlock()

			return nil
		}
		s.mu.Lock()
		s.deliveries = append(s.deliveries, rec)
		s.mu.Unlock()

		return nil
	default:
		return fmt.Errorf("unknown via %q", via)
	}
}

func (s *Server) controlPing(w http.ResponseWriter, r *http.Request) {
	var req pingControlRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil && err != io.EOF {
		writeError(w, http.StatusBadRequest, "invalid ping control body: "+err.Error())

		return
	}

	s.mu.Lock()
	if req.Period != nil {
		s.period = *req.Period
	}
	if req.NewServer != nil {
		s.newServer = *req.NewServer
	}
	if req.SyncTime != nil {
		s.syncTime = *req.SyncTime
	}
	if req.RotateToken != nil {
		s.rotateToken = *req.RotateToken
	}
	for _, cmd := range req.Commands {
		s.httpCommands = append(s.httpCommands, cmd)
		s.deliveries = append(s.deliveries, DeliveryRecord{
			At:     time.Now().UTC(),
			Id:     cmd.Id,
			Via:    "http",
			Status: "queued",
		})
	}
	s.mu.Unlock()

	writeJSON(w, http.StatusOK, map[string]any{"ok": true})
}

func (s *Server) controlKick(w http.ResponseWriter, r *http.Request) {
	s.mu.Lock()
	conns := make([]*wsConn, 0, len(s.conns))
	for _, c := range s.conns {
		conns = append(conns, c)
	}
	s.kicks++
	s.mu.Unlock()

	for _, c := range conns {
		_ = c.conn.CloseNow()
	}
	writeJSON(w, http.StatusOK, map[string]any{"kicked": len(conns)})
}

func (s *Server) controlEventsFail(w http.ResponseWriter, r *http.Request) {
	var req eventsFailRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil && err != io.EOF {
		writeError(w, http.StatusBadRequest, "invalid events-fail body: "+err.Error())

		return
	}
	s.mu.Lock()
	s.eventsFail = req.Count
	if req.Status > 0 {
		s.eventFailStatus = req.Status
	}
	s.mu.Unlock()

	writeJSON(w, http.StatusOK, map[string]any{"ok": true})
}

func (s *Server) controlWS(w http.ResponseWriter, r *http.Request) {
	var req wsControlRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil && err != io.EOF {
		writeError(w, http.StatusBadRequest, "invalid ws control body: "+err.Error())

		return
	}
	s.mu.Lock()
	if req.Disabled != nil {
		s.disableWS = *req.Disabled
	}
	s.mu.Unlock()

	writeJSON(w, http.StatusOK, map[string]any{"ok": true})
}

func (s *Server) controlUpgrade(w http.ResponseWriter, r *http.Request) {
	var req upgradeControlRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil && err != io.EOF {
		writeError(w, http.StatusBadRequest, "invalid upgrade control body: "+err.Error())

		return
	}
	s.mu.Lock()
	if req.Clear {
		s.upgradeInfo = nil
	} else {
		info := req.UpgradeInfo
		s.upgradeInfo = &info
	}
	s.mu.Unlock()

	writeJSON(w, http.StatusOK, map[string]any{"ok": true})
}

// CloseWSConnections is a helper for in-process tests.
func (s *Server) CloseWSConnections() {
	s.mu.Lock()
	conns := make([]*wsConn, 0, len(s.conns))
	for _, c := range s.conns {
		conns = append(conns, c)
	}
	s.mu.Unlock()
	for _, c := range conns {
		_ = c.conn.CloseNow()
	}
}
