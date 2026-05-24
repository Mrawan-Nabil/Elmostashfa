// =============================================================================
// EventBus.js
// A simple publish/subscribe system.
// One part of the app can "emit" an event, and any other part that called "on"
// for that event will be notified automatically.
// =============================================================================

export class EventBus {
  constructor() {
    // Store all listeners grouped by event name
    this.listeners = {};
  }

  // Register a callback to run whenever the given event is emitted
  on(event, callback) {
    if (!this.listeners[event]) this.listeners[event] = [];
    this.listeners[event].push(callback);
  }

  // Fire an event and pass data to every registered callback
  emit(event, data) {
    if (this.listeners[event]) {
      for (const cb of this.listeners[event]) cb(data);
    }
  }
}
