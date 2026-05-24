// =============================================================================
// PriorityQueue.js
// A sorted list where the item with the highest priority always comes first.
// The sort order is controlled by a comparator function passed in at creation.
// =============================================================================

export class PriorityQueue {
  // comparator(a, b) should return a negative number if a comes before b
  constructor(comparator) {
    this._items = [];
    this._cmp   = comparator;
  }

  // Add an item and re-sort so the highest priority item stays at the front
  enqueue(item) {
    this._items.push(item);
    this.reorder();
  }

  // Remove and return the highest priority item (front of the list)
  dequeue() {
    return this._items.shift();
  }

  // Look at the highest priority item without removing it
  peek() {
    return this._items[0];
  }

  // Remove the first item that matches the given test function
  remove(predicate) {
    const idx = this._items.findIndex(predicate);
    if (idx !== -1) {
      return this._items.splice(idx, 1)[0];
    }
    return null;
  }

  // Re-sort the list — call this if an item's priority changed externally
  reorder() {
    this._items.sort(this._cmp);
  }

  // Return a copy of the internal array in priority order
  toArray() {
    return [...this._items];
  }

  get size()    { return this._items.length; }
  get isEmpty() { return this._items.length === 0; }
}
