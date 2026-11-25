# 0028 — Secret values are encrypted at rest with envelope keys

- Status: accepted
- Date: 2026-11-20
- Context: Some values (tax numbers, third-party keys) must be unreadable to anyone with the database or a backup.

## Decision

A secret property is mapped with `IsSecret`: an EF value converter around `ISecretProtector`. Each value gets a fresh AES-256-GCM data key, wrapped by the current key-encryption key from `Secrets:Keys`; the envelope names the key that wraps it, so rotation only adds a key. The domain sees plaintext.

## What varies

The keys, their rotation, which members are secret.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Database encryption at rest | Nothing in code | Anyone with a connection or a dump reads plaintext |
| One key encrypting every value | Simpler | Rotation means re-encrypting everything at once |
| **Envelope keys with the key id inside** | A converter and keys in a secret store | n/a |

## Consequences

- Gained: dumps and backups hold envelopes; keys rotate without downtime.
- Lost or owed: secret columns cannot be searched or indexed by value; losing a key loses its values.
- Cost of going back: high (values are only readable with the keys).

## How it is enforced

`EnvelopeSecretProtectorTests`, `CustomerConfigurationTests`, and `secrets.sh` (the column never holds the plaintext; the API reads it back).
