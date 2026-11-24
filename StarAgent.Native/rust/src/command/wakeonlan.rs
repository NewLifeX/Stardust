//! Wake-on-LAN：魔术包构造与局域网广播。
//!
//! 广播只能在二层广播域内生效，因此由目标所在局域网内的在线节点代发；
//! 单张网卡或单个目标发送失败不影响其它网卡。

use std::net::{Ipv4Addr, UdpSocket};

use anyhow::{Context, Result};

/// Wake-on-LAN 常用 UDP 端口。
const WAKE_PORTS: [u16; 2] = [9, 7];

/// 解析 MAC 列表。
///
/// 支持 `AA-BB-CC-DD-EE-FF`、`aa:bb:cc:dd:ee:ff`、`aabb.ccdd.eeff`，
/// 多个用逗号、分号或空白分隔；非法项忽略，重复项去重。
pub fn parse_macs(text: &str) -> Vec<[u8; 6]> {
    let mut list = Vec::new();
    for item in text.split(|c: char| c == ',' || c == ';' || c.is_whitespace()) {
        let hex_text: String = item
            .chars()
            .filter(|c| *c != '-' && *c != ':' && *c != '.')
            .collect();
        if hex_text.len() != 12 {
            continue;
        }

        let mut mac = [0u8; 6];
        let mut valid = true;
        for i in 0..6 {
            match u8::from_str_radix(&hex_text[i * 2..i * 2 + 2], 16) {
                Ok(value) => mac[i] = value,
                Err(_) => {
                    valid = false;
                    break;
                }
            }
        }
        if valid && !list.contains(&mac) {
            list.push(mac);
        }
    }

    list
}

/// 构造魔术包：6 个 `0xFF` 加 16 次目标 MAC，共 102 字节。
pub fn build_magic_packet(mac: [u8; 6]) -> Vec<u8> {
    let mut packet = vec![0u8; 6 + 16 * 6];
    packet[..6].fill(0xFF);
    for i in 0..16 {
        packet[6 + i * 6..6 + (i + 1) * 6].copy_from_slice(&mac);
    }

    packet
}

/// 向本机所有局域网网卡广播魔术包，返回发送的报文数量。
pub fn send(macs: &[[u8; 6]]) -> Result<usize> {
    if macs.is_empty() {
        return Ok(0);
    }

    let interfaces = get_if_addrs::get_if_addrs().context("枚举网卡失败")?;
    let socket = UdpSocket::bind((Ipv4Addr::UNSPECIFIED, 0)).context("创建 UDP 套接字失败")?;
    socket.set_broadcast(true).context("启用广播失败")?;

    let mut sent = 0;
    let mut last_error: Option<std::io::Error> = None;
    for interface in interfaces {
        if interface.is_loopback() {
            continue;
        }
        let get_if_addrs::IfAddr::V4(addr) = interface.addr else {
            continue;
        };
        // 自动专用地址（169.254.0.0/16）通常来自未连网网卡，广播出去没有意义
        if addr.ip.is_loopback() || addr.ip.is_link_local() {
            continue;
        }

        let broadcast = addr.broadcast.unwrap_or_else(|| {
            let ip = u32::from(addr.ip);
            let mask = u32::from(addr.netmask);
            Ipv4Addr::from(ip | !mask)
        });
        let targets = [broadcast, Ipv4Addr::BROADCAST];
        for mac in macs {
            let packet = build_magic_packet(*mac);
            for port in WAKE_PORTS {
                for target in targets {
                    match socket.send_to(&packet, (target, port)) {
                        Ok(_) => sent += 1,
                        Err(error) => last_error = Some(error),
                    }
                }
            }
        }
    }

    // 部分网卡成功时忽略个别失败，全部失败才把错误交给调用方
    if sent > 0 {
        return Ok(sent);
    }
    match last_error {
        Some(error) => Err(error.into()),
        None => Ok(0),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn magic_packet_layout() {
        let mac = [0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF];

        let packet = build_magic_packet(mac);

        assert_eq!(102, packet.len());
        assert!(packet[..6].iter().all(|value| *value == 0xFF));
        for i in 0..16 {
            assert_eq!(&mac[..], &packet[6 + i * 6..6 + (i + 1) * 6]);
        }
    }

    #[test]
    fn parses_multiple_formats_and_deduplicates() {
        let list =
            parse_macs("AA-BB-CC-DD-EE-FF, aa:bb:cc:dd:ee:ff; aabb.ccdd.eeff 11-22-33-44-55-66");

        assert_eq!(2, list.len());
        assert_eq!([0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF], list[0]);
        assert_eq!([0x11, 0x22, 0x33, 0x44, 0x55, 0x66], list[1]);
    }

    #[test]
    fn ignores_invalid_macs() {
        for text in [
            "",
            "ZZ-BB-CC-DD-EE-FF",
            "AA-BB-CC-DD-EE",
            "AA-BB-CC-DD-EE-FF-00",
            "hello world",
        ] {
            assert!(parse_macs(text).is_empty(), "text = {text}");
        }
    }
}
