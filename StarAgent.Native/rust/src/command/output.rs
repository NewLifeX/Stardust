//! Bounded head/tail output capture and UTF-8 safe truncation.

use std::collections::VecDeque;

use crate::protocol::MAX_OUTPUT_BYTES;

/// Marker inserted when output is truncated.
pub const TRUNCATION_MARKER: &str = "\n...<output truncated>...\n";

fn valid_prefix(bytes: &[u8], want: usize) -> String {
    let mut end = want.min(bytes.len());
    while end > 0 && std::str::from_utf8(&bytes[..end]).is_err() {
        end -= 1;
    }
    String::from_utf8_lossy(&bytes[..end]).into_owned()
}

fn valid_suffix(bytes: &[u8], want: usize) -> String {
    let mut start = bytes.len().saturating_sub(want);
    while start < bytes.len() && std::str::from_utf8(&bytes[start..]).is_err() {
        start += 1;
    }
    String::from_utf8_lossy(&bytes[start..]).into_owned()
}

/// Truncates text to [`MAX_OUTPUT_BYTES`], keeping the head and the tail.
pub fn truncate_output(text: &str) -> String {
    let bytes = text.as_bytes();
    if bytes.len() <= MAX_OUTPUT_BYTES {
        return text.to_string();
    }

    let keep = (MAX_OUTPUT_BYTES - TRUNCATION_MARKER.len()) / 2;
    let head = valid_prefix(bytes, keep);
    let tail = valid_suffix(bytes, keep);
    format!("{head}{TRUNCATION_MARKER}{tail}")
}

/// Captures the first and last bytes of a stream without unbounded growth.
pub struct HeadTailBuffer {
    head_limit: usize,
    tail_limit: usize,
    head: Vec<u8>,
    tail: VecDeque<u8>,
    total: usize,
}

impl HeadTailBuffer {
    /// Creates a buffer capped at `max` bytes split between head and tail.
    pub fn new(max: usize) -> Self {
        Self {
            head_limit: max / 2,
            tail_limit: max - max / 2,
            head: Vec::new(),
            tail: VecDeque::new(),
            total: 0,
        }
    }

    /// Appends a chunk.
    pub fn push(&mut self, mut data: &[u8]) {
        self.total += data.len();

        if self.head.len() < self.head_limit {
            let take = (self.head_limit - self.head.len()).min(data.len());
            self.head.extend_from_slice(&data[..take]);
            data = &data[take..];
        }

        if !data.is_empty() {
            self.tail.extend(data.iter().copied());
            while self.tail.len() > self.tail_limit {
                self.tail.pop_front();
            }
        }
    }

    /// Returns true when some output was dropped.
    pub fn truncated(&self) -> bool {
        self.total > self.head_limit + self.tail_limit
    }

    /// Renders the captured output.
    pub fn render(&self) -> String {
        let head = String::from_utf8_lossy(&self.head).into_owned();
        if !self.truncated() {
            let mut value = head;
            value.push_str(&String::from_utf8_lossy(
                &self.tail.iter().copied().collect::<Vec<u8>>(),
            ));
            return value;
        }

        let tail_bytes: Vec<u8> = self.tail.iter().copied().collect();
        let head_text = {
            let mut end = self.head.len();
            while end > 0 && std::str::from_utf8(&self.head[..end]).is_err() {
                end -= 1;
            }
            String::from_utf8_lossy(&self.head[..end]).into_owned()
        };
        let mut start = 0;
        while start < tail_bytes.len() && std::str::from_utf8(&tail_bytes[start..]).is_err() {
            start += 1;
        }
        let tail_text = String::from_utf8_lossy(&tail_bytes[start..]).into_owned();

        format!("{head_text}{TRUNCATION_MARKER}{tail_text}")
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn keeps_small_output() {
        let text = "hello\nworld";
        assert_eq!(truncate_output(text), text);
    }

    #[test]
    fn truncates_large_output_to_limit() {
        let text = "a".repeat(MAX_OUTPUT_BYTES * 2);
        let result = truncate_output(&text);
        assert!(result.len() <= MAX_OUTPUT_BYTES);
        assert!(result.contains("<output truncated>"));
        assert!(result.starts_with('a'));
        assert!(result.ends_with('a'));
    }

    #[test]
    fn truncates_on_utf8_boundaries() {
        let text = "中文".repeat(MAX_OUTPUT_BYTES);
        let result = truncate_output(&text);
        assert!(result.len() <= MAX_OUTPUT_BYTES);
        assert!(std::str::from_utf8(result.as_bytes()).is_ok());
        assert!(!result.contains('\u{FFFD}'));
    }

    #[test]
    fn buffer_keeps_head_and_tail() {
        let mut buffer = HeadTailBuffer::new(10);
        buffer.push(b"0123456789ABCDEFGHIJ");
        assert!(buffer.truncated());
        let text = buffer.render();
        assert!(text.starts_with("01234"));
        assert!(text.ends_with("FGHIJ"));
        assert!(text.contains("<output truncated>"));
    }

    #[test]
    fn buffer_preserves_short_output() {
        let mut buffer = HeadTailBuffer::new(100);
        buffer.push(b"hello ");
        buffer.push(b"world");
        assert!(!buffer.truncated());
        assert_eq!(buffer.render(), "hello world");
    }
}
