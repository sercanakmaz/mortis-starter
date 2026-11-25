# 0016 — Messages answer in the caller's language; translated data is stored per language

- Status: accepted
- Date: 2026-11-22
- Context: Users of this service speak more than one language. They should read failures in their own language, and some business data (a product's name) exists in several languages.

## Decision

The language of a call is the signed-in user's language claim, then the request's `Accept-Language`, limited to the supported languages with the default as the fallback; the Console takes the claim of its token. It travels on `CommandContext.Language` and on every event. A failure carries a code and its parameters; the answer's message is looked up at the edge in `messages.<language>.json` and falls back to the default language. A translated field is a `LocalizedText` value object stored as one `jsonb` column: a write stores one text under the caller's language, a read returns the caller's language (falling back to the default) and every translation. The supported languages are generated constants.

## What varies

Which languages are supported, and which fields are translated.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Messages in the code, one language | Nothing | Users read failures in a language not theirs |
| .NET `.resx` resources | Designer files, culture-named assemblies | Harder to edit and review than one JSON file per language |
| A table per translated field | A join per read | More schema than one JSON column |
| **Codes with parameters, a JSON file per language, `LocalizedText` in jsonb** | Two small classes, a converter | n/a |

## Consequences

- Gained: one failure answers in each user's language; a name is stored once per language; a consumer runs in the language of the request that caused it.
- Lost or owed: field messages of validation failures are not translated; searching by a translated field needs its JSON path and, for speed, an index per language.
- Cost of going back: moderate.

## How it is enforced

`MessageLocalizerTests` (every code has a text in every language), `LanguagesTests`, `LocalizedTextTests`, `ExceptionHandlerTests`, and `languages.sh`.
