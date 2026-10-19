package stardust

import (
	"bytes"
	"compress/gzip"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/http/httptest"
	"net/url"
	"strings"
	"sync"
	"sync/atomic"
	"testing"
	"time"
)

type capturedRequest struct {
	Path        string
	Query       url.Values
	ContentType string
	Token       string
	Body        []byte
}

type requestRecorder struct {
	mu   sync.Mutex
	reqs []capturedRequest
}

func (r *requestRecorder) add(req capturedRequest) {
	r.mu.Lock()
	r.reqs = append(r.reqs, req)
	r.mu.Unlock()
}

func (r *requestRecorder) all() []capturedRequest {
	r.mu.Lock()
	defer r.mu.Unlock()

	out := make([]capturedRequest, len(r.reqs))
	copy(out, r.reqs)
	return out
}

func (r *requestRecorder) filter(path string) []capturedRequest {
	var out []capturedRequest
	for _, req := range r.all() {
		if req.Path == path {
			out = append(out, req)
		}
	}
	return out
}

// newRecordedServer 创建记录请求的测试服务。Cleanup 按注册顺序逆序执行，
// 因此先注册服务关闭，再由后注册的 Tracer.Stop 先停止客户端。
func newRecordedServer(t *testing.T, handler func(w http.ResponseWriter, req capturedRequest)) (*httptest.Server, *requestRecorder) {
	t.Helper()

	rec := &requestRecorder{}
	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		body, _ := io.ReadAll(r.Body)
		req := capturedRequest{
			Path:        r.URL.Path,
			Query:       r.URL.Query(),
			ContentType: r.Header.Get("Content-Type"),
			Token:       r.URL.Query().Get("Token"),
			Body:        body,
		}
		rec.add(req)
		handler(w, req)
	}))
	t.Cleanup(srv.Close)

	return srv, rec
}

func newTracerForTest(t *testing.T, server string) *Tracer {
	t.Helper()

	tracer := NewTracer(server, "TestApp", "TestSecret")
	tracer.SetHTTPClient(&http.Client{Timeout: 2 * time.Second})
	t.Cleanup(tracer.Stop)
	return tracer
}

func writeRawJSON(w http.ResponseWriter, value interface{}) {
	w.Header().Set("Content-Type", "application/json")
	_ = json.NewEncoder(w).Encode(value)
}

func writeAPI(w http.ResponseWriter, data interface{}) {
	writeRawJSON(w, map[string]interface{}{"code": 0, "data": data})
}

func writeAPIError(w http.ResponseWriter, code int, message string) {
	writeRawJSON(w, map[string]interface{}{"code": code, "message": message})
}

func decodePayload(t *testing.T, body []byte, target interface{}) {
	t.Helper()
	if err := json.Unmarshal(body, target); err != nil {
		t.Fatalf("unmarshal request body failed: %v, body=%s", err, string(body))
	}
}

// decodeReportRequest 读取上报请求体，自动处理 GZip 压缩
func decodeReportRequest(t *testing.T, req capturedRequest) reportPayload {
	t.Helper()

	body := req.Body
	if req.ContentType == "application/x-gzip" {
		gz, err := gzip.NewReader(bytes.NewReader(body))
		if err != nil {
			t.Fatalf("gzip reader failed: %v", err)
		}
		defer gz.Close()

		body, err = io.ReadAll(gz)
		if err != nil {
			t.Fatalf("gzip read failed: %v", err)
		}
	}

	var payload reportPayload
	decodePayload(t, body, &payload)
	return payload
}

type reportPayload struct {
	AppID    string                 `json:"AppId"`
	AppName  string                 `json:"AppName"`
	ClientID string                 `json:"ClientId"`
	Version  string                 `json:"Version"`
	Info     map[string]interface{} `json:"Info"`
	Builders []struct {
		Name  string `json:"Name"`
		Total int    `json:"Total"`
	} `json:"Builders"`
}

func TestLoginSendsVersionAndIP(t *testing.T) {
	srv, rec := newRecordedServer(t, func(w http.ResponseWriter, req capturedRequest) {
		if req.Path == "/App/Login" {
			writeAPI(w, map[string]interface{}{
				"Token":  "token-1",
				"Expire": 7200,
			})
			return
		}
		http.Error(w, "unexpected path", http.StatusNotFound)
	})

	tracer := newTracerForTest(t, srv.URL)
	tracer.SetVersion("3.7.2026.0701").SetIP("192.168.1.100")

	if !tracer.login() {
		t.Fatal("login failed")
	}

	token, expire := tracer.tokenSnapshot()
	if token != "token-1" {
		t.Errorf("Expected token-1, got %s", token)
	}
	if expire < time.Now().Unix()+7000 {
		t.Errorf("Expected tokenExpire ~= now+7200, got %d", expire)
	}

	reqs := rec.filter("/App/Login")
	if len(reqs) != 1 {
		t.Fatalf("Expected 1 login request, got %d", len(reqs))
	}

	var payload map[string]interface{}
	decodePayload(t, reqs[0].Body, &payload)

	if payload["AppId"] != "TestApp" {
		t.Errorf("Expected AppId=TestApp, got %v", payload["AppId"])
	}
	if payload["Secret"] != "TestSecret" {
		t.Errorf("Expected Secret=TestSecret, got %v", payload["Secret"])
	}
	if payload["AppName"] != "TestApp" {
		t.Errorf("Expected AppName=TestApp, got %v", payload["AppName"])
	}
	if payload["ClientId"] == "" {
		t.Error("Expected non-empty ClientId")
	}
	if payload["Version"] != "3.7.2026.0701" {
		t.Errorf("Expected Version=3.7.2026.0701, got %v", payload["Version"])
	}
	if payload["IP"] != "192.168.1.100" {
		t.Errorf("Expected IP=192.168.1.100, got %v", payload["IP"])
	}
}

func TestPingSendsAppInfo(t *testing.T) {
	srv, rec := newRecordedServer(t, func(w http.ResponseWriter, req capturedRequest) {
		if req.Path == "/App/Ping" {
			writeAPI(w, map[string]interface{}{"Time": time.Now().UnixMilli()})
			return
		}
		http.Error(w, "unexpected path", http.StatusNotFound)
	})

	tracer := newTracerForTest(t, srv.URL)
	tracer.SetVersion("3.7.2026.0701").SetIP("192.168.1.100")
	tracer.setTokenWithExpire("token-1", time.Now().Unix()+3600)

	tracer.ping()

	reqs := rec.filter("/App/Ping")
	if len(reqs) != 1 {
		t.Fatalf("Expected 1 ping request, got %d", len(reqs))
	}
	if reqs[0].Token != "token-1" {
		t.Errorf("Expected token-1, got %s", reqs[0].Token)
	}

	var payload map[string]interface{}
	decodePayload(t, reqs[0].Body, &payload)

	if payload["Name"] != "TestApp" {
		t.Errorf("Expected Name=TestApp, got %v", payload["Name"])
	}
	if payload["Version"] != "3.7.2026.0701" {
		t.Errorf("Expected Version=3.7.2026.0701, got %v", payload["Version"])
	}
	if payload["IP"] != "192.168.1.100" {
		t.Errorf("Expected IP=192.168.1.100, got %v", payload["IP"])
	}
	if id, ok := payload["Id"].(float64); !ok || id <= 0 {
		t.Errorf("Expected numeric Id > 0, got %v", payload["Id"])
	}
	if threads, ok := payload["Threads"].(float64); !ok || threads < 1 {
		t.Errorf("Expected numeric Threads >= 1, got %v", payload["Threads"])
	}
	if workingSet, ok := payload["WorkingSet"].(float64); !ok || workingSet <= 0 {
		t.Errorf("Expected numeric WorkingSet > 0, got %v", payload["WorkingSet"])
	}
	if _, ok := payload["HeapSize"].(float64); !ok {
		t.Errorf("Expected numeric HeapSize, got %v", payload["HeapSize"])
	}
	if _, ok := payload["GCCount"].(float64); !ok {
		t.Errorf("Expected numeric GCCount, got %v", payload["GCCount"])
	}
	if now, ok := payload["Time"].(float64); !ok || now <= 0 {
		t.Errorf("Expected numeric Time > 0, got %v", payload["Time"])
	}
	if payload["CommandLine"] == "" {
		t.Error("Expected non-empty CommandLine")
	}
	if startTime, ok := payload["StartTime"].(string); !ok {
		t.Errorf("Expected string StartTime, got %v", payload["StartTime"])
	} else if _, err := time.Parse(time.RFC3339, startTime); err != nil {
		t.Errorf("StartTime should be RFC3339: %v", err)
	}
}

func TestPingRenewsToken(t *testing.T) {
	srv, rec := newRecordedServer(t, func(w http.ResponseWriter, req capturedRequest) {
		if req.Path == "/App/Ping" {
			writeAPI(w, map[string]interface{}{"Token": "renewed-token"})
			return
		}
		http.Error(w, "unexpected path", http.StatusNotFound)
	})

	tracer := newTracerForTest(t, srv.URL)
	tracer.setTokenWithExpire("old-token", time.Now().Unix()+3600)

	tracer.ping()

	if len(rec.filter("/App/Login")) != 0 {
		t.Error("Should not login when current token is valid")
	}
	if len(rec.filter("/App/Ping")) != 1 {
		t.Error("Expected 1 ping request")
	}
	if got := tracer.getToken(); got != "renewed-token" {
		t.Errorf("Expected renewed-token, got %s", got)
	}
}

func TestExpiringTokenReloginBeforePing(t *testing.T) {
	srv, rec := newRecordedServer(t, func(w http.ResponseWriter, req capturedRequest) {
		switch req.Path {
		case "/App/Login":
			writeAPI(w, map[string]interface{}{"Token": "fresh-token", "Expire": 7200})
		case "/App/Ping":
			writeAPI(w, map[string]interface{}{"Time": time.Now().UnixMilli()})
		default:
			http.Error(w, "unexpected path", http.StatusNotFound)
		}
	})

	tracer := newTracerForTest(t, srv.URL)
	// 距离过期不足 5 分钟，应先重新登录
	tracer.setTokenWithExpire("expiring-token", time.Now().Unix()+60)

	tracer.ping()

	if len(rec.filter("/App/Login")) != 1 {
		t.Error("Expected re-login for expiring token")
	}
	reqs := rec.filter("/App/Ping")
	if len(reqs) != 1 {
		t.Fatalf("Expected 1 ping request, got %d", len(reqs))
	}
	if reqs[0].Token != "fresh-token" {
		t.Errorf("Expected fresh-token, got %s", reqs[0].Token)
	}
}

func TestReportSendsVersionInfoAndBuilders(t *testing.T) {
	srv, rec := newRecordedServer(t, func(w http.ResponseWriter, req capturedRequest) {
		switch req.Path {
		case "/Trace/Report", "/Trace/ReportRaw":
			writeAPI(w, map[string]interface{}{
				"Period":       15,
				"MaxSamples":   2,
				"MaxErrors":    20,
				"Timeout":      3000,
				"MaxTagLength": 512,
				"EnableMeter":  true,
				"Excludes":     []string{"/health"},
			})
		default:
			http.Error(w, "unexpected path", http.StatusNotFound)
		}
	})

	tracer := newTracerForTest(t, srv.URL)
	tracer.SetVersion("2.0.0").SetIP("10.0.0.8")
	tracer.setTokenWithExpire("token-1", time.Now().Unix()+3600)

	span := tracer.NewSpan("BizOp", "")
	span.Tag = "hello"
	span.Finish()
	tracer.flush()

	reqs := append(rec.filter("/Trace/Report"), rec.filter("/Trace/ReportRaw")...)
	if len(reqs) != 1 {
		t.Fatalf("Expected 1 report request, got %d", len(reqs))
	}
	if reqs[0].Token != "token-1" {
		t.Errorf("Expected token-1, got %s", reqs[0].Token)
	}
	if reqs[0].ContentType != "application/json" && reqs[0].ContentType != "application/x-gzip" {
		t.Errorf("Unexpected content type %s", reqs[0].ContentType)
	}

	payload := decodeReportRequest(t, reqs[0])

	if payload.AppID != "TestApp" {
		t.Errorf("Expected AppId=TestApp, got %s", payload.AppID)
	}
	if payload.Version != "2.0.0" {
		t.Errorf("Expected Version=2.0.0, got %s", payload.Version)
	}
	if payload.Info == nil {
		t.Fatal("Expected Info when EnableMeter=true")
	}
	if _, ok := payload.Info["Threads"].(float64); !ok {
		t.Errorf("Expected numeric Info.Threads, got %v", payload.Info["Threads"])
	}
	if len(payload.Builders) != 1 {
		t.Fatalf("Expected 1 builder, got %d", len(payload.Builders))
	}
	if payload.Builders[0].Name != "BizOp" {
		t.Errorf("Expected builder BizOp, got %s", payload.Builders[0].Name)
	}
	if payload.Builders[0].Total != 1 {
		t.Errorf("Expected builder Total=1, got %d", payload.Builders[0].Total)
	}

	// 服务端下发的采样参数应生效
	if got := tracer.getPeriod(); got != 15 {
		t.Errorf("Expected Period=15, got %d", got)
	}
	if tracer.MaxSamples != 2 {
		t.Errorf("Expected MaxSamples=2, got %d", tracer.MaxSamples)
	}
	if tracer.MaxErrors != 20 {
		t.Errorf("Expected MaxErrors=20, got %d", tracer.MaxErrors)
	}
	if tracer.Timeout != 3000 {
		t.Errorf("Expected Timeout=3000, got %d", tracer.Timeout)
	}
	if tracer.MaxTagLen != 512 {
		t.Errorf("Expected MaxTagLen=512, got %d", tracer.MaxTagLen)
	}
	if len(tracer.Excludes) != 1 || tracer.Excludes[0] != "/health" {
		t.Errorf("Expected Excludes=[/health], got %v", tracer.Excludes)
	}
}

func TestReportWithoutMeterOmitsInfo(t *testing.T) {
	srv, rec := newRecordedServer(t, func(w http.ResponseWriter, req capturedRequest) {
		switch req.Path {
		case "/Trace/Report", "/Trace/ReportRaw":
			writeAPI(w, map[string]interface{}{})
		default:
			http.Error(w, "unexpected path", http.StatusNotFound)
		}
	})

	tracer := newTracerForTest(t, srv.URL)
	tracer.SetEnableMeter(false)
	tracer.setTokenWithExpire("token-1", time.Now().Unix()+3600)

	span := tracer.NewSpan("BizOp", "")
	span.Finish()
	tracer.flush()

	reqs := append(rec.filter("/Trace/Report"), rec.filter("/Trace/ReportRaw")...)
	if len(reqs) != 1 {
		t.Fatalf("Expected 1 report request, got %d", len(reqs))
	}

	payload := decodeReportRequest(t, reqs[0])
	if payload.Info != nil {
		t.Errorf("Expected no Info when EnableMeter=false, got %v", payload.Info)
	}
	if len(payload.Builders) != 1 {
		t.Errorf("Expected 1 builder, got %d", len(payload.Builders))
	}
}

func TestReportGzipWhenLarge(t *testing.T) {
	srv, rec := newRecordedServer(t, func(w http.ResponseWriter, req capturedRequest) {
		if req.Path == "/Trace/ReportRaw" {
			writeAPI(w, map[string]interface{}{"Period": 30})
			return
		}
		http.Error(w, "unexpected path", http.StatusNotFound)
	})

	tracer := newTracerForTest(t, srv.URL)
	tracer.SetVersion("2.0.0")
	tracer.setTokenWithExpire("token-1", time.Now().Unix()+3600)

	span := tracer.NewSpan("LargeOp", "")
	span.Tag = strings.Repeat("x", 2048)
	span.Finish()
	tracer.flush()

	if len(rec.filter("/Trace/Report")) != 0 {
		t.Error("Large payload should not use /Trace/Report")
	}
	reqs := rec.filter("/Trace/ReportRaw")
	if len(reqs) != 1 {
		t.Fatalf("Expected 1 ReportRaw request, got %d", len(reqs))
	}
	if reqs[0].ContentType != "application/x-gzip" {
		t.Errorf("Expected application/x-gzip, got %s", reqs[0].ContentType)
	}
	if reqs[0].Token != "token-1" {
		t.Errorf("Expected token-1, got %s", reqs[0].Token)
	}

	gz, err := gzip.NewReader(bytes.NewReader(reqs[0].Body))
	if err != nil {
		t.Fatalf("gzip reader failed: %v", err)
	}
	defer gz.Close()

	plain, err := io.ReadAll(gz)
	if err != nil {
		t.Fatalf("gzip read failed: %v", err)
	}

	var payload reportPayload
	decodePayload(t, plain, &payload)
	if payload.Version != "2.0.0" {
		t.Errorf("Expected Version=2.0.0, got %s", payload.Version)
	}
	if len(payload.Builders) != 1 || payload.Builders[0].Name != "LargeOp" {
		t.Errorf("Expected LargeOp builder, got %+v", payload.Builders)
	}

	if got := tracer.getPeriod(); got != 30 {
		t.Errorf("Expected Period=30, got %d", got)
	}
}

func TestUnauthorizedTriggersRelogin(t *testing.T) {
	for _, mode := range []string{"http401", "api401"} {
		t.Run(mode, func(t *testing.T) {
			var logins int32
			srv, rec := newRecordedServer(t, func(w http.ResponseWriter, req capturedRequest) {
				switch req.Path {
				case "/App/Login":
					n := atomic.AddInt32(&logins, 1)
					writeAPI(w, map[string]interface{}{
						"Token":  fmt.Sprintf("token-%d", n),
						"Expire": 7200,
					})
				case "/App/Ping":
					if req.Token == "token-1" {
						if mode == "http401" {
							w.WriteHeader(http.StatusUnauthorized)
							_, _ = w.Write([]byte(`{"code":401,"message":"unauthorized"}`))
						} else {
							writeAPIError(w, http.StatusUnauthorized, "unauthorized")
						}
						return
					}
					writeAPI(w, map[string]interface{}{"Time": time.Now().UnixMilli()})
				default:
					http.Error(w, "unexpected path", http.StatusNotFound)
				}
			})

			tracer := newTracerForTest(t, srv.URL)
			tracer.ping()

			if got := len(rec.filter("/App/Login")); got != 2 {
				t.Errorf("Expected 2 login requests, got %d", got)
			}
			pingReqs := rec.filter("/App/Ping")
			if len(pingReqs) != 2 {
				t.Fatalf("Expected 2 ping requests, got %d", len(pingReqs))
			}
			if pingReqs[0].Token != "token-1" {
				t.Errorf("Expected first ping token-1, got %s", pingReqs[0].Token)
			}
			if pingReqs[1].Token != "token-2" {
				t.Errorf("Expected retry ping token-2, got %s", pingReqs[1].Token)
			}
			if got := tracer.getToken(); got != "token-2" {
				t.Errorf("Expected token-2 after retry, got %s", got)
			}
		})
	}
}

func TestStartStopExitsPromptly(t *testing.T) {
	srv, rec := newRecordedServer(t, func(w http.ResponseWriter, req capturedRequest) {
		switch req.Path {
		case "/App/Login":
			writeAPI(w, map[string]interface{}{"Token": "token-1", "Expire": 7200})
		case "/App/Ping":
			writeAPI(w, map[string]interface{}{"Time": time.Now().UnixMilli()})
		case "/Trace/Report":
			writeAPI(w, map[string]interface{}{"Period": 60})
		default:
			http.Error(w, "unexpected path", http.StatusNotFound)
		}
	})

	tracer := newTracerForTest(t, srv.URL)
	tracer.SetPeriod(3600)
	tracer.Start()

	if got := len(rec.filter("/App/Login")); got != 1 {
		t.Fatalf("Expected 1 login request on Start, got %d", got)
	}

	done := make(chan struct{})
	go func() {
		tracer.Stop()
		close(done)
	}()

	select {
	case <-done:
	case <-time.After(2 * time.Second):
		t.Fatal("Stop did not exit promptly")
	}
}
