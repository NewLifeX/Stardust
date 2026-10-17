package scenario

import (
	"fmt"
	"strings"

	"staragent-conformance/internal/fakeserver"
	"staragent-conformance/internal/protocol"
)

func countReplies(st *fakeserver.State, id int64) int {
	if st == nil {
		return 0
	}
	n := 0
	for _, reply := range st.Replies {
		if reply.Reply.Id == id {
			n++
		}
	}

	return n
}

func repliesFor(st *fakeserver.State, id int64) []fakeserver.ReplyRecord {
	if st == nil {
		return nil
	}
	out := make([]fakeserver.ReplyRecord, 0, 1)
	for _, reply := range st.Replies {
		if reply.Reply.Id == id {
			out = append(out, reply)
		}
	}

	return out
}

func countDeliveries(st *fakeserver.State, id int64) int {
	if st == nil {
		return 0
	}
	n := 0
	for _, d := range st.Deliveries {
		if d.Id == id {
			n++
		}
	}

	return n
}

func commandDeliveredInPing(st *fakeserver.State, id int64) bool {
	if st == nil {
		return false
	}
	for _, ping := range st.Pings {
		for _, cmdId := range ping.CommandIds {
			if cmdId == id {
				return true
			}
		}
	}

	return false
}

func summarizeState(st *fakeserver.State) string {
	if st == nil {
		return "no state (agent may not have contacted the server)"
	}

	return fmt.Sprintf("logins=%d pings=%d replies=%d events=%d attempts=%d ws=%d active=%d pongs=%d kicks=%d",
		len(st.Logins), len(st.Pings), len(st.Replies), len(st.Events), len(st.EventAttempts),
		st.WSConnections, st.WSActive, st.Pongs, st.Kicks)
}

func summarizeRequests(st *fakeserver.State, limit int) string {
	if st == nil || len(st.Requests) == 0 {
		return "none"
	}
	start := 0
	if limit > 0 && len(st.Requests) > limit {
		start = len(st.Requests) - limit
	}
	parts := make([]string, 0, len(st.Requests)-start)
	for _, req := range st.Requests[start:] {
		item := fmt.Sprintf("%s %s %d", req.Method, req.Path, req.Status)
		if req.Query != "" {
			item += "?" + req.Query
		}
		parts = append(parts, item)
	}

	return strings.Join(parts, "; ")
}

func summarizeReplies(st *fakeserver.State, limit int) string {
	if st == nil || len(st.Replies) == 0 {
		return "none"
	}
	start := 0
	if limit > 0 && len(st.Replies) > limit {
		start = len(st.Replies) - limit
	}
	parts := make([]string, 0, len(st.Replies)-start)
	for _, rec := range st.Replies[start:] {
		parts = append(parts, fmt.Sprintf("id=%d status=%d data=%q", rec.Reply.Id, rec.Reply.Status, protocol.Truncate(rec.Reply.Data, 80)))
	}

	return strings.Join(parts, "; ")
}

func summarizeEventAttempts(st *fakeserver.State) string {
	if st == nil || len(st.EventAttempts) == 0 {
		return "none"
	}
	statuses := make([]string, 0, len(st.EventAttempts))
	for _, attempt := range st.EventAttempts {
		statuses = append(statuses, fmt.Sprint(attempt.Status))
	}

	return strings.Join(statuses, ",")
}

func summarizeDeliveries(st *fakeserver.State) string {
	if st == nil || len(st.Deliveries) == 0 {
		return "none"
	}
	parts := make([]string, 0, len(st.Deliveries))
	for _, d := range st.Deliveries {
		parts = append(parts, fmt.Sprintf("id=%d via=%s %s", d.Id, d.Via, d.Status))
	}

	return strings.Join(parts, "; ")
}

func indentTail(text string, maxLines int) string {
	lines := strings.Split(strings.TrimRight(text, "\r\n"), "\n")
	if maxLines > 0 && len(lines) > maxLines {
		lines = lines[len(lines)-maxLines:]
	}
	for i, line := range lines {
		lines[i] = "    " + strings.TrimRight(line, "\r")
	}

	return strings.Join(lines, "\n")
}
