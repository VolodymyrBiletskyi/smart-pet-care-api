# Chat–Classifier Contract v1

Status: accepted as the integration contract for the C# backend.

This document is the source of truth for communication between the Smart Pet
Care C# backend and the classifier service. The C# types in
`Infrastructure/Classifier/Contracts/ClassifierChatContracts.cs` implement
this contract.

## Responsibilities

### C# backend owns

- user authentication and authorization;
- conversation and pet ownership checks;
- creation and persistence of conversations and messages;
- selection and chronological replay of conversation history;
- persistence of the latest symptom summary and user/assistant messages;
- mapping classifier failures to the public API;
- the public `/api/sessions` response contracts.

### Classifier owns

- interpreting the supplied messages and optional pet context;
- choosing the response mode;
- generating the user-facing answer, disclaimer and related topics;
- maintaining and returning the latest symptom summary;
- generating prediction, urgency, specialist and home-advice data when applicable;
- validating classifier-specific input and returning an appropriate HTTP status.

### Classifier does not own

- authentication or authorization of Smart Pet Care users;
- conversation history or durable session state;
- database writes to the C# backend;
- concurrency control for conversations.

The classifier must be stateless and side-effect-free. `sessionId` is a
correlation identifier only; it must not be used as the classifier's source of
conversation history.

## Transport

- Method: `POST`
- Relative path: `chat`
- Content type: `application/json`
- Authentication header: `X-API-Key: <configured key>`
- JSON property naming: camel case
- Null request properties are omitted
- Enum values are serialized exactly as documented below

The final URL is resolved relative to `Classifier:BaseUrl`. Non-local
deployments must use HTTPS.

## Request

```json
{
  "sessionId": "9197ec8f-17a6-476c-a747-f3ecf9df1134",
  "messages": [
    {
      "role": "user",
      "content": "My dog has not eaten since yesterday."
    }
  ],
  "symptomSummary": "Reduced appetite since yesterday.",
  "petType": "dog"
}
```

| Field | Requirement | Meaning |
|---|---|---|
| `sessionId` | optional UUID string | Correlation identifier assigned by the C# backend. |
| `messages` | required array | Chronological replay ending with the current user message. The C# backend sends at most 8 entries. |
| `messages[].role` | required enum | `user` or `assistant`. |
| `messages[].content` | required string | Message text. The public API limits the current user message to 4,000 characters. |
| `symptomSummary` | optional string | Latest summary returned by the classifier for this conversation. |
| `petType` | optional enum | `dog`, `cat`, `rabbit`, `hamster`, `guinea_pig`, `bird`, `fish`, `turtle`, or `other`. |

The C# backend owns history ordering and truncation. The classifier must use
only the supplied request as conversational context.

## Successful response

```json
{
  "mode": "health",
  "answer": "Reduced appetite can have several causes...",
  "symptomSummary": "Dog has had reduced appetite since yesterday.",
  "prediction": {
    "predictedCondition": "Gastrointestinal upset",
    "confidence": 0.72,
    "topK": [
      {
        "condition": "Gastrointestinal upset",
        "confidence": 0.72
      }
    ],
    "urgency": "CONSULT_SOON",
    "specialist": "Veterinarian",
    "diseaseCategory": "Digestive",
    "homeAdvice": [
      "Ensure fresh water is available."
    ]
  },
  "relatedTopics": [
    "hydration",
    "appetite monitoring"
  ],
  "needsClarification": false,
  "disclaimer": "This guidance does not replace a veterinary examination."
}
```

| Field | Requirement | Meaning |
|---|---|---|
| `mode` | required enum | `general`, `health`, or `emergency`. |
| `answer` | required string | User-facing assistant response. |
| `symptomSummary` | required string | Complete latest summary; use an empty string when no health summary applies. |
| `prediction` | optional object | Structured health prediction. Omit or return `null` when not applicable. |
| `relatedTopics` | required string array | Related subjects; return an empty array when none apply. |
| `needsClarification` | required boolean | Whether another user answer is needed before a useful assessment. |
| `disclaimer` | required string | User-facing safety disclaimer. |

When `prediction` is present, all its scalar fields and both arrays are
required. `urgency` must be one of `MONITOR`, `CONSULT_SOON`, `URGENT`, or
`EMERGENCY`. Confidence values must be finite JSON numbers. Empty `topK` and
`homeAdvice` arrays are valid.

The classifier must return a complete `symptomSummary`, not a patch. The C#
backend replaces the previously persisted summary with this value.

## Failure semantics

| Classifier outcome | C# interpretation | Public session-message API |
|---|---|---|
| Valid 2xx response | Successful turn | `200 OK` |
| `422 Unprocessable Entity` | Generated classifier request was rejected | `502 Bad Gateway` |
| Other 4xx response | Unexpected classifier contract failure | `502 Bad Gateway` |
| 5xx response | Classifier temporarily unavailable | `503 Service Unavailable` |
| Network or response-read failure | Classifier unavailable | `503 Service Unavailable` |
| Configured HTTP timeout | Classifier unavailable | `503 Service Unavailable` |
| Malformed or contract-invalid 2xx body | Invalid classifier response | `502 Bad Gateway` |
| Caller cancellation | Request is cancelled | No classifier-specific remapping |

The C# backend persists the user's source message before calling the
classifier. When the classifier call fails, it does not add an assistant
message or overwrite the last valid symptom summary. Transport failures are not
retried automatically.

## Persistence and privacy

The C# backend stores:

- the user message;
- the user-facing classifier answer;
- the latest symptom summary on the conversation.

Message text, symptom summaries, API keys and raw classifier response bodies
must not be written to normal application logs. Logs may contain identifiers,
status codes, durations, replay counts and retry attempt numbers.

## Compatibility policy

This is contract version 1. Changes that remove fields, rename fields, change
enum wire values, make optional fields required, or alter field meaning require
a new contract version and coordinated deployment.

The following changes are backward-compatible within v1:

- adding optional response fields that the C# backend can ignore;
- adding new diagnostics that do not change the response body;
- relaxing classifier-side validation without changing field meaning.

New enum values are not backward-compatible because the C# backend rejects
unknown values. They require a coordinated C# change before the classifier can
emit them.
