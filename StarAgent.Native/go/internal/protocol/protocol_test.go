package protocol

import (
	"encoding/json"
	"errors"
	"os"
	"path/filepath"
	"testing"
	"time"
)

type basicVectors struct {
	LoginRequest  json.RawMessage `json:"loginRequest"`
	LoginResponse json.RawMessage `json:"loginResponse"`
	PingRequest   json.RawMessage `json:"pingRequest"`
	PingResponse  json.RawMessage `json:"pingResponse"`
	CommandReply  json.RawMessage `json:"commandReply"`
	Event         json.RawMessage `json:"event"`
	UpgradeInfo   json.RawMessage `json:"upgradeInfo"`
}

func loadVectors(t *testing.T) basicVectors {
	t.Helper()

	path := filepath.Join("..", "..", "..", "spec", "vectors", "basic.json")
	data, err := os.ReadFile(path)
	if err != nil {
		t.Fatalf("read vectors: %v", err)
	}

	var vectors basicVectors
	if err := json.Unmarshal(data, &vectors); err != nil {
		t.Fatalf("decode vectors: %v", err)
	}

	return vectors
}

func TestBasicVectors(t *testing.T) {
	vectors := loadVectors(t)

	var login LoginInfo
	if err := Unmarshal(vectors.LoginRequest, &login); err != nil {
		t.Fatalf("login request: %v", err)
	}
	if login.ProductCode != "StarAgent-Go" || login.Node.MachineName != "web-01" {
		t.Fatalf("unexpected login request: %+v", login)
	}
	if login.Node.Compile.String() != "2026-10-17 08:00:00" {
		t.Fatalf("unexpected compile: %s", login.Node.Compile)
	}
	if len(login.Node.Plugins) != 4 {
		t.Fatalf("unexpected plugins: %v", login.Node.Plugins)
	}

	var loginResp LoginResponse
	if err := Unmarshal(vectors.LoginResponse, &loginResp); err != nil {
		t.Fatalf("login response: %v", err)
	}
	if loginResp.Token != "jwt-token" || loginResp.Expire != 7200 || loginResp.Code != "A1B2C3D4" {
		t.Fatalf("unexpected login response: %+v", loginResp)
	}

	var ping PingInfo
	if err := Unmarshal(vectors.PingRequest, &ping); err != nil {
		t.Fatalf("ping request: %v", err)
	}
	if ping.Time != 1760000000000 || ping.CpuRate != 12.5 || ping.ProcessCount != 120 {
		t.Fatalf("unexpected ping request: %+v", ping)
	}

	var pingResp PingResponse
	if err := Unmarshal(vectors.PingResponse, &pingResp); err != nil {
		t.Fatalf("ping response: %v", err)
	}
	if pingResp.Period != 60 || len(pingResp.Commands) != 1 {
		t.Fatalf("unexpected ping response: %+v", pingResp)
	}
	cmd := pingResp.Commands[0]
	if cmd.Id != 123 || cmd.Command != "bash" || cmd.Status != StatusReady {
		t.Fatalf("unexpected command: %+v", cmd)
	}
	if cmd.StartTime.IsSet() || cmd.Expire.IsSet() {
		t.Fatalf("zero command times should not be set: %+v", cmd)
	}

	var reply CommandReplyModel
	if err := Unmarshal(vectors.CommandReply, &reply); err != nil {
		t.Fatalf("command reply: %v", err)
	}
	if reply.Id != 123 || reply.Status != StatusCompleted || reply.Data == "" {
		t.Fatalf("unexpected reply: %+v", reply)
	}

	var event EventModel
	if err := Unmarshal(vectors.Event, &event); err != nil {
		t.Fatalf("event: %v", err)
	}
	if event.Type != "info" || event.Name != "Startup" {
		t.Fatalf("unexpected event: %+v", event)
	}

	var upgrade UpgradeInfo
	if err := Unmarshal(vectors.UpgradeInfo, &upgrade); err != nil {
		t.Fatalf("upgrade: %v", err)
	}
	if upgrade.Version != "1.1.2026.1101" || upgrade.FileSize != 12345678 {
		t.Fatalf("unexpected upgrade: %+v", upgrade)
	}
}

func TestTimeFormats(t *testing.T) {
	cases := []struct {
		input string
		want  time.Time
	}{
		{`"2026-10-17 10:00:00"`, time.Date(2026, 10, 17, 10, 0, 0, 0, time.UTC)},
		{`"2026-10-17T10:00:00Z"`, time.Date(2026, 10, 17, 10, 0, 0, 0, time.UTC)},
		{`"2026-10-17T10:00:00.1234567Z"`, time.Date(2026, 10, 17, 10, 0, 0, 123456700, time.UTC)},
		{`"0001-01-01 00:00:00"`, time.Time{}},
		{`null`, time.Time{}},
		{`""`, time.Time{}},
	}

	for _, c := range cases {
		var got Time
		if err := json.Unmarshal([]byte(c.input), &got); err != nil {
			t.Fatalf("unmarshal %s: %v", c.input, err)
		}
		if !got.Equal(c.want) {
			t.Fatalf("unmarshal %s = %v, want %v", c.input, got.Time, c.want)
		}
	}

	var invalid Time
	if err := json.Unmarshal([]byte(`"not-a-time"`), &invalid); err == nil {
		t.Fatal("expected invalid time error")
	}
}

func TestTimeMarshalUsesUTC(t *testing.T) {
	local := time.Date(2026, 10, 17, 18, 0, 0, 0, time.FixedZone("CST", 8*3600))
	got, err := json.Marshal(NewTime(local))
	if err != nil {
		t.Fatalf("marshal: %v", err)
	}
	if string(got) != `"2026-10-17 10:00:00"` {
		t.Fatalf("unexpected marshal: %s", got)
	}

	got, err = json.Marshal(Time{})
	if err != nil {
		t.Fatalf("marshal zero: %v", err)
	}
	if string(got) != `"0001-01-01 00:00:00"` {
		t.Fatalf("unexpected zero marshal: %s", got)
	}
}

func TestUnknownAndMissingFields(t *testing.T) {
	var resp PingResponse
	data := []byte(`{"Period":30,"UnknownFutureField":{"nested":true},"Token":null,"Commands":null}`)
	if err := Unmarshal(data, &resp); err != nil {
		t.Fatalf("unknown fields should be ignored: %v", err)
	}
	if resp.Period != 30 || resp.Token != "" || len(resp.Commands) != 0 {
		t.Fatalf("unexpected response: %+v", resp)
	}

	var empty LoginResponse
	if err := Unmarshal([]byte(`{}`), &empty); err != nil {
		t.Fatalf("empty object: %v", err)
	}
	if empty.Expire != 0 || empty.ServerTime != 0 {
		t.Fatalf("missing numbers should be zero: %+v", empty)
	}
}

func TestCaseInsensitiveFields(t *testing.T) {
	var resp LoginResponse
	if err := Unmarshal([]byte(`{"code":"C1","TOKEN":"t1","expire":60,"servertime":99}`), &resp); err != nil {
		t.Fatalf("unmarshal: %v", err)
	}
	if resp.Code != "C1" || resp.Token != "t1" || resp.Expire != 60 || resp.ServerTime != 99 {
		t.Fatalf("case-insensitive decode failed: %+v", resp)
	}
}

func TestParseWrappedResponse(t *testing.T) {
	var resp LoginResponse
	if err := ParseResponse([]byte(`{"code":0,"data":{"Token":"abc","Expire":120},"msg":"ok"}`), &resp); err != nil {
		t.Fatalf("wrapped: %v", err)
	}
	if resp.Token != "abc" || resp.Expire != 120 {
		t.Fatalf("wrapped decode failed: %+v", resp)
	}

	if err := ParseResponse([]byte(`{"Token":"bare"}`), &resp); err != nil {
		t.Fatalf("bare: %v", err)
	}
	if resp.Token != "bare" {
		t.Fatalf("bare decode failed: %+v", resp)
	}

	var apiErr *APIError
	err := ParseResponse([]byte(`{"code":5,"data":null,"msg":"denied"}`), &resp)
	if !errors.As(err, &apiErr) || apiErr.Code != 5 || apiErr.Msg != "denied" {
		t.Fatalf("expected APIError, got %v", err)
	}
}

func TestParseCount(t *testing.T) {
	cases := map[string]int{
		`3`:                            3,
		`"4"`:                          4,
		`{"code":0,"data":5,"msg":""}`: 5,
		`{"code":0,"data":"6"}`:        6,
	}
	for input, want := range cases {
		got, err := ParseCount([]byte(input))
		if err != nil {
			t.Fatalf("ParseCount(%s): %v", input, err)
		}
		if got != want {
			t.Fatalf("ParseCount(%s) = %d, want %d", input, got, want)
		}
	}

	if _, err := ParseCount([]byte(`{"code":7,"msg":"bad"}`)); err == nil {
		t.Fatal("expected error for non-zero code")
	}
	if _, err := ParseCount([]byte(`[]`)); err == nil {
		t.Fatal("expected error for unsupported payload")
	}
}
