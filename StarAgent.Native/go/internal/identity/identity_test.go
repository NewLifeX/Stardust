package identity

import (
	"strings"
	"testing"
)

func TestFormatMacs(t *testing.T) {
	got := formatMacs([]string{"aa:bb:cc:dd:ee:ff", "00:11:22:33:44:55", "aa:bb:cc:dd:ee:ff"})
	if got != "00-11-22-33-44-55,AA-BB-CC-DD-EE-FF" {
		t.Fatalf("formatMacs = %q", got)
	}
	if formatMacs(nil) != "" {
		t.Fatal("empty macs should produce empty string")
	}
}

func TestCleanID(t *testing.T) {
	cases := map[string]string{
		`"ABC"`:                                "ABC",
		"  abc  ":                              "abc",
		"To be filled by O.E.M.":               "",
		"00000000-0000-0000-0000-000000000000": "",
		"Default string":                       "",
	}
	for input, want := range cases {
		if got := cleanID(input); got != want {
			t.Fatalf("cleanID(%q) = %q, want %q", input, got, want)
		}
	}
}

func TestArchitecture(t *testing.T) {
	got := Architecture()
	if got == "" || strings.ToLower(got) == "unknown" {
		t.Fatalf("unexpected architecture: %q", got)
	}
}

func TestTruncateDNS(t *testing.T) {
	short := "223.5.5.5,8.8.8.8"
	if got := truncateDNS(short); got != short {
		t.Fatalf("truncateDNS(%q) = %q", short, got)
	}
	long := strings.Repeat("1", 60)
	if got := truncateDNS(long); len(got) != 50 {
		t.Fatalf("truncateDNS length = %d, want 50", len(got))
	}
}

func TestNormalizeWindowsVersion(t *testing.T) {
	cases := map[string]string{
		"10.0.19045":                       "10.0.19045",
		"10.0.19045.6456":                  "10.0.19045",
		"10.0.19045.6456 Build 19045.6456": "10.0.19045",
	}
	for input, want := range cases {
		if got := normalizeWindowsVersion(input); got != want {
			t.Fatalf("normalizeWindowsVersion(%q) = %q, want %q", input, got, want)
		}
	}
}

func TestTimeZoneName(t *testing.T) {
	if got := TimeZoneName(); got == "" || got == "Local" {
		t.Fatalf("unexpected timezone: %q", got)
	}
}

func TestParseKeyValues(t *testing.T) {
	values := parseKeyValues("UUID=abc\nSERIAL=123\nNOPE\n")
	if values["UUID"] != "abc" || values["SERIAL"] != "123" || len(values) != 2 {
		t.Fatalf("unexpected values: %+v", values)
	}
}
