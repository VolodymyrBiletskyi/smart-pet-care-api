# Chat session API

All endpoints require bearer authentication and only expose sessions owned by
the current user.

## Paginated message history

```http
GET /api/sessions/{sessionId}/messages?limit=8&cursor={nextCursor}
```

- `limit` defaults to 8 and must be between 1 and 8.
- Omit `cursor` for the newest page.
- Pass the previous response's `nextCursor` to load older messages.
- Items are returned in chronological order within each page.
- The endpoint does not call the classifier.

Example response:

```json
{
  "sessionId": "15471e5c-f3a7-4238-b109-aca0ce7c267f",
  "items": [
    {
      "messageId": "34d07aae-0ee5-4aa4-ab23-f1d635e79910",
      "role": "user",
      "content": "My dog is lethargic.",
      "createdAt": "2026-07-15T08:01:00Z"
    }
  ],
  "pagination": {
    "limit": 8,
    "hasMore": true,
    "nextCursor": "opaque-cursor"
  }
}
```

When no older messages remain, `hasMore` is false and `nextCursor` is null.
An invalid limit or cursor returns 400. A missing or unowned session returns
404.
