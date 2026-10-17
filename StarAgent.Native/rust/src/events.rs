//! Persistent bounded event queue.

use std::collections::VecDeque;
use std::path::PathBuf;
use std::sync::Mutex;

use tracing::warn;

use crate::config::write_atomic;
use crate::protocol::EventModel;

/// Default queue capacity.
pub const MAX_EVENTS: usize = 1000;

/// In-memory event queue persisted to disk.
pub struct EventQueue {
    path: Option<PathBuf>,
    max: usize,
    events: Mutex<VecDeque<EventModel>>,
}

impl EventQueue {
    /// Creates a queue and restores persisted events when possible.
    pub fn new(path: Option<PathBuf>, max: usize) -> Self {
        let max = if max == 0 { MAX_EVENTS } else { max };
        let mut events = VecDeque::new();

        if let Some(file) = &path {
            match std::fs::read(file) {
                Ok(data) if !data.is_empty() => {
                    match serde_json::from_slice::<Vec<EventModel>>(&data) {
                        Ok(loaded) => events = loaded.into(),
                        Err(error) => {
                            warn!(path = %file.display(), error = %error, "event queue load failed, starting empty")
                        }
                    }
                }
                Ok(_) => {}
                Err(error) if error.kind() == std::io::ErrorKind::NotFound => {}
                Err(error) => {
                    warn!(path = %file.display(), error = %error, "event queue read failed, starting empty")
                }
            }
        }

        while events.len() > max {
            events.pop_front();
        }

        Self {
            path,
            max,
            events: Mutex::new(events),
        }
    }

    /// Appends one event and persists the queue.
    pub fn add(&self, event: EventModel) {
        let mut events = self.lock();
        events.push_back(event);
        while events.len() > self.max {
            events.pop_front();
        }
        self.save(&events);
    }

    /// Takes up to `count` events from the front.
    pub fn take(&self, count: usize) -> Vec<EventModel> {
        let mut events = self.lock();
        if events.is_empty() || count == 0 {
            return Vec::new();
        }
        let count = count.min(events.len());
        let batch: Vec<EventModel> = events.drain(..count).collect();
        self.save(&events);
        batch
    }

    /// Puts failed events back at the front.
    pub fn put_front(&self, batch: &[EventModel]) {
        if batch.is_empty() {
            return;
        }
        let mut events = self.lock();
        for event in batch.iter().rev() {
            events.push_front(event.clone());
        }
        while events.len() > self.max {
            events.pop_back();
        }
        self.save(&events);
    }

    /// Returns the queue length.
    pub fn len(&self) -> usize {
        self.lock().len()
    }

    /// Returns true when the queue is empty.
    pub fn is_empty(&self) -> bool {
        self.len() == 0
    }

    fn lock(&self) -> std::sync::MutexGuard<'_, VecDeque<EventModel>> {
        self.events
            .lock()
            .unwrap_or_else(|poison| poison.into_inner())
    }

    fn save(&self, events: &VecDeque<EventModel>) {
        let Some(path) = &self.path else {
            return;
        };
        match serde_json::to_vec(events) {
            Ok(data) => {
                if let Err(error) = write_atomic(path, &data, 0o600) {
                    warn!(path = %path.display(), error = %error, "event queue persist failed");
                }
            }
            Err(error) => warn!(error = %error, "event queue encode failed"),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn event(name: &str) -> EventModel {
        EventModel {
            time: 1,
            event_type: "info".into(),
            name: name.into(),
            remark: None,
        }
    }

    #[test]
    fn queues_and_takes_events() {
        let queue = EventQueue::new(None, 3);
        queue.add(event("a"));
        queue.add(event("b"));
        assert_eq!(queue.len(), 2);

        let batch = queue.take(1);
        assert_eq!(batch.len(), 1);
        assert_eq!(batch[0].name, "a");
        assert_eq!(queue.len(), 1);
    }

    #[test]
    fn trims_to_capacity() {
        let queue = EventQueue::new(None, 2);
        queue.add(event("a"));
        queue.add(event("b"));
        queue.add(event("c"));
        assert_eq!(queue.len(), 2);
        let batch = queue.take(2);
        assert_eq!(batch[0].name, "b");
        assert_eq!(batch[1].name, "c");
    }

    #[test]
    fn persists_and_restores() {
        let dir = tempfile::tempdir().unwrap();
        let path = dir.path().join("events.json");
        {
            let queue = EventQueue::new(Some(path.clone()), 10);
            queue.add(event("startup"));
        }

        let restored = EventQueue::new(Some(path), 10);
        assert_eq!(restored.len(), 1);
        let batch = restored.take(10);
        assert_eq!(batch[0].name, "startup");
        assert!(restored.is_empty());
    }

    #[test]
    fn puts_events_back_at_the_front() {
        let queue = EventQueue::new(None, 10);
        queue.add(event("b"));
        queue.put_front(&[event("a")]);
        let batch = queue.take(2);
        assert_eq!(batch[0].name, "a");
        assert_eq!(batch[1].name, "b");
    }
}
