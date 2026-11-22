# Languages of messages and data

Two separate things people mean by "multi-language": the messages the system answers with, in the caller's language, and business data stored in several languages. The code of one language is in its pages (`languages.md`).

## The language of a call

```
host receives a request:
  candidates := [the signed-in caller's language claim, then the request's accepted languages by preference]
  context.language := the first candidate that is supported (or whose primary tag is: "tr-TR" -> "tr"), else the default
console run:  the language claim of its token, else the default
event:        carries context.language; a consumer's command runs in it
```

- The supported languages and the default are fixed when the project is generated, not settings.
- A profile language (the claim) comes before the browser's; an unsupported language falls back to the default.

## Messages

```
failure := (code, parameters)                   never a sentence written at the throw
at the edge (HTTP answer, console error line):
  text := messages[context.language][code] ?? messages[default][code] ?? the failure's own message
  fill {parameter} from parameters
```

- One message file per language, keyed by code. A test fails when a code has no text in a supported language.
- The log line keeps the failure's own message in the code's language; only the answer to the caller is translated.
- Needs coded failures (R5): without codes there is nothing to look up.

## Data

```
LocalizedText := { language -> text }            a value object, stored as one document/JSON value
get(language, fallback) := text[language] ?? text[fallback] ?? any text
write of one text:  name := name.with(context.language, text)
read:               name := name.get(context.language, default); names := every translation
```

- A command still sends one text; it lands under the caller's language. Renaming in another language adds that language and keeps the others.
- Searching or sorting by a translated field uses the language's path inside the stored value; an index per language makes it fast and is added by hand when a query needs it.
- A read whose answer depends on the language carries the caller's context like a command does.
