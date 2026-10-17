package fakeserver

import (
	"time"

	"staragent-conformance/internal/protocol"
)

// RequestRecord captures one HTTP request handled by the fake server.
type RequestRecord struct {
	At       time.Time `json:"at"`
	Method   string    `json:"method"`
	Path     string    `json:"path"`
	Query    string    `json:"query,omitempty"`
	Body     string    `json:"body,omitempty"`
	Status   int       `json:"status"`
	HasAuth  bool      `json:"hasAuth"`
	Remote   string    `json:"remote,omitempty"`
	Duration int64     `json:"durationMs"`
}

// LoginRecord captures one POST /node/login call.
type LoginRecord struct {
	At             time.Time          `json:"at"`
	Code           string             `json:"code,omitempty"`
	Secret         string             `json:"secret,omitempty"`
	ProductCode    string             `json:"productCode,omitempty"`
	ClientId       string             `json:"clientId,omitempty"`
	Project        string             `json:"project,omitempty"`
	Node           *protocol.NodeInfo `json:"node,omitempty"`
	Status         int                `json:"status"`
	ResponseCode   string             `json:"responseCode,omitempty"`
	ResponseSecret string             `json:"responseSecret,omitempty"`
	ResponseToken  string             `json:"responseToken,omitempty"`
	Error          string             `json:"error,omitempty"`
	Remote         string             `json:"remote,omitempty"`
}

// PingRecord captures one POST /node/ping call and its response commands.
type PingRecord struct {
	At           time.Time `json:"at"`
	TimeField    int64     `json:"timeField"`
	IP           string    `json:"ip,omitempty"`
	CpuRate      float64   `json:"cpuRate"`
	Memory       int64     `json:"memory"`
	ProcessCount int       `json:"processCount"`
	CommandIds   []int64   `json:"commandIds,omitempty"`
	Status       int       `json:"status"`
	Error        string    `json:"error,omitempty"`
}

// ReplyRecord captures one POST /node/commandreply call.
type ReplyRecord struct {
	At     time.Time                  `json:"at"`
	Reply  protocol.CommandReplyModel `json:"reply"`
	Status int                        `json:"status"`
	Error  string                     `json:"error,omitempty"`
}

// EventRecord is one event accepted by the server.
type EventRecord struct {
	At        time.Time           `json:"at"`
	Event     protocol.EventModel `json:"event"`
	BatchSize int                 `json:"batchSize"`
}

// EventAttempt records every POST /node/postevents attempt, including failures.
type EventAttempt struct {
	At     time.Time             `json:"at"`
	Status int                   `json:"status"`
	Count  int                   `json:"count"`
	Events []protocol.EventModel `json:"events,omitempty"`
	Error  string                `json:"error,omitempty"`
}

// UpgradeRecord captures one POST /node/upgrade call.
type UpgradeRecord struct {
	At      time.Time `json:"at"`
	Channel string    `json:"channel,omitempty"`
	Status  int       `json:"status"`
}

// LogoutRecord captures one POST /node/logout call.
type LogoutRecord struct {
	At     time.Time `json:"at"`
	Reason string    `json:"reason,omitempty"`
	Status int       `json:"status"`
}

// FrameRecord is one WebSocket text/binary frame in either direction.
type FrameRecord struct {
	At      time.Time `json:"at"`
	ConnId  int       `json:"connId"`
	Dir     string    `json:"dir"`  // sent | recv
	Type    string    `json:"type"` // text | binary
	Payload string    `json:"payload,omitempty"`
}

// DeliveryRecord tracks a command enqueued through the control API.
type DeliveryRecord struct {
	At     time.Time `json:"at"`
	Id     int64     `json:"id"`
	Via    string    `json:"via"`    // ws | http
	Status string    `json:"status"` // sent | queued | error
	Error  string    `json:"error,omitempty"`
}

// State is a point-in-time snapshot exposed by GET /control/state.
type State struct {
	URL          string `json:"url"`
	WSURL        string `json:"wsUrl"`
	Registered   bool   `json:"registered"`
	IssuedCode   string `json:"issuedCode,omitempty"`
	IssuedSecret string `json:"issuedSecret,omitempty"`
	Token        string `json:"token,omitempty"`
	Period       int    `json:"period"`
	DisableWS    bool   `json:"disableWs"`

	Logins        []LoginRecord    `json:"logins"`
	Pings         []PingRecord     `json:"pings"`
	Replies       []ReplyRecord    `json:"replies"`
	Events        []EventRecord    `json:"events"`
	EventAttempts []EventAttempt   `json:"eventAttempts"`
	Upgrades      []UpgradeRecord  `json:"upgrades"`
	Logouts       []LogoutRecord   `json:"logouts"`
	Requests      []RequestRecord  `json:"requests"`
	Deliveries    []DeliveryRecord `json:"deliveries"`
	Frames        []FrameRecord    `json:"frames"`

	WSConnections      int `json:"wsConnections"`
	WSActive           int `json:"wsActive"`
	Pongs              int `json:"pongs"`
	Kicks              int `json:"kicks"`
	EventFailRemaining int `json:"eventFailRemaining"`
}

func newState(s *Server) *State {
	s.mu.Lock()
	defer s.mu.Unlock()

	st := &State{
		URL:                s.url,
		WSURL:              wsURLOf(s.url),
		Registered:         s.registered,
		IssuedCode:         s.issuedCode,
		IssuedSecret:       s.issuedSecret,
		Token:              s.token,
		Period:             s.period,
		DisableWS:          s.disableWS,
		WSConnections:      s.wsConnections,
		WSActive:           s.wsActive,
		Pongs:              s.pongs,
		Kicks:              s.kicks,
		EventFailRemaining: s.eventsFail,
	}
	st.Logins = append([]LoginRecord(nil), s.logins...)
	st.Pings = append([]PingRecord(nil), s.pings...)
	st.Replies = append([]ReplyRecord(nil), s.replies...)
	st.Events = append([]EventRecord(nil), s.events...)
	st.EventAttempts = append([]EventAttempt(nil), s.eventAttempts...)
	st.Upgrades = append([]UpgradeRecord(nil), s.upgrades...)
	st.Logouts = append([]LogoutRecord(nil), s.logouts...)
	st.Requests = append([]RequestRecord(nil), s.requests...)
	st.Deliveries = append([]DeliveryRecord(nil), s.deliveries...)
	st.Frames = append([]FrameRecord(nil), s.frames...)

	return st
}
