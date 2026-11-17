# Languages of messages and data

Whether messages or stored data are translated.

## languages.mode

What is translated.

**By preset:** lean `none`, standard `none`, advanced `data`.

### none

**Choose when:** one language.

**Costs:** none.

### messages

**Choose when:** error and validation messages are shown to users in their language.

**Costs:** a code and parameters for every message, resource files per language.

**Rules:** R5.

### data

**Choose when:** stored values such as product names exist per language.

**Costs:** a value per language in storage and a fallback rule.

**Rules:** R5.

Example:

```
| Key | Value | Why |
| languages.mode | none |  |
```

## languages.default

The language used when none is asked for. A language tag such as `en` or `pt-BR`. Only when `languages.mode` is not `none`.

**Default:** `en`.

Example:

```
| Key | Value | Why |
| languages.default | en |  |
```

## languages.supported

The languages that have translations, comma separated. Must include the default. Only when `languages.mode` is not `none`.

**Default:** `en`.

Example:

```
| Key | Value | Why |
| languages.supported | en, tr |  |
```
