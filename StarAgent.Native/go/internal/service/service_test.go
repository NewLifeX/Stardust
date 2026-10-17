package service

import (
	"strings"
	"testing"
)

func TestGenerateSystemdUnit(t *testing.T) {
	unit := GenerateSystemdUnit(InstallOptions{
		InstallDir:  "/opt/staragent",
		ConfigPath:  "/opt/staragent/staragent.json",
		ServiceUser: "staragent",
	})
	for _, want := range []string{
		"Description=StarAgent Native Agent",
		"ExecStart=/opt/staragent/staragent run -config /opt/staragent/staragent.json",
		"Restart=always",
		"KillSignal=SIGTERM",
		"User=staragent",
		"WantedBy=multi-user.target",
	} {
		if !strings.Contains(unit, want) {
			t.Fatalf("unit missing %q:\n%s", want, unit)
		}
	}
}

func TestGenerateSystemdUnitQuotesSpaces(t *testing.T) {
	unit := GenerateSystemdUnit(InstallOptions{
		InstallDir: "/opt/star agent",
		ConfigPath: "/opt/star agent/staragent.json",
	})
	if !strings.Contains(unit, `ExecStart="/opt/star agent/staragent" run -config "/opt/star agent/staragent.json"`) {
		t.Fatalf("paths with spaces not quoted:\n%s", unit)
	}
}

func TestGenerateOpenWrtScript(t *testing.T) {
	script := GenerateOpenWrtScript(InstallOptions{
		InstallDir: "/usr/local/staragent",
		ConfigPath: "/etc/staragent.json",
	})
	for _, want := range []string{"#!/bin/sh /etc/rc.common", "USE_PROCD=1", "procd_open_instance", "procd_set_param respawn", "procd_kill"} {
		if !strings.Contains(script, want) {
			t.Fatalf("script missing %q:\n%s", want, script)
		}
	}
}

func TestGenerateLaunchdPlist(t *testing.T) {
	plist := GenerateLaunchdPlist(InstallOptions{
		InstallDir: "/usr/local/staragent",
		ConfigPath: "/usr/local/staragent/staragent.json",
	}, "/usr/local/staragent/logs")
	for _, want := range []string{"<string>com.newlifex.staragent</string>", "<key>KeepAlive</key>", "<string>run</string>", "launchd.out.log"} {
		if !strings.Contains(plist, want) {
			t.Fatalf("plist missing %q:\n%s", want, plist)
		}
	}
}
