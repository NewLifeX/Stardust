package protocol

import (
	"net/http"
	"testing"
	"time"
)

func TestDateTime(t *testing.T) {
	now := time.Date(2026, 10, 17, 8, 30, 5, 0, time.UTC)
	text := FormatDateTime(now)
	if text != "2026-10-17 08:30:05" {
		t.Fatalf("FormatDateTime = %q", text)
	}
	if !IsDateTime(text) {
		t.Fatalf("IsDateTime(%q) = false", text)
	}
	parsed := ParseDateTime(text)
	if !parsed.Equal(now) {
		t.Fatalf("ParseDateTime = %s, want %s", parsed, now)
	}
	if !ParseDateTime(ZeroDateTime).IsZero() {
		t.Fatalf("%q must parse as zero time", ZeroDateTime)
	}
	if !ParseDateTime("").IsZero() {
		t.Fatal("empty string must parse as zero time")
	}
	if !ParseDateTime("not-a-time").IsZero() {
		t.Fatal("invalid string must parse as zero time")
	}
	if IsDateTime("2026-10-17T08:30:05Z") {
		t.Fatal("RFC3339 must not be accepted as the wire format")
	}
}

func TestBearerToken(t *testing.T) {
	r, _ := http.NewRequest(http.MethodGet, "http://example.com", nil)
	r.Header.Set("Authorization", "Bearer abc")
	if got := BearerToken(r); got != "abc" {
		t.Fatalf("BearerToken = %q", got)
	}
	r.Header.Set("Authorization", "bearer xyz")
	if got := BearerToken(r); got != "xyz" {
		t.Fatalf("BearerToken (lowercase) = %q", got)
	}
	r.Header.Set("Authorization", "Basic abc")
	if got := BearerToken(r); got != "" {
		t.Fatalf("BearerToken (basic) = %q", got)
	}
}

func TestParseEventCount(t *testing.T) {
	cases := []struct {
		input string
		want  int
		ok    bool
	}{
		{"3", 3, true},
		{`{"code":0,"data":2,"msg":""}`, 2, true},
		{`{"code":0,"data":0}`, 0, true},
		{`"x"`, 0, false},
	}
	for _, c := range cases {
		got, err := ParseEventCount([]byte(c.input))
		if c.ok && err != nil {
			t.Fatalf("ParseEventCount(%q) error: %v", c.input, err)
		}
		if !c.ok && err == nil {
			t.Fatalf("ParseEventCount(%q) unexpectedly succeeded", c.input)
		}
		if got != c.want {
			t.Fatalf("ParseEventCount(%q) = %d, want %d", c.input, got, c.want)
		}
	}
}
