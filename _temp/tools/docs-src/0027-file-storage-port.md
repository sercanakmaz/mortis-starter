# 0027 — Files live behind a storage port; rows hold keys

- Status: accepted
- Date: <the date the project was generated>
- Context: Files are uploaded and served. Bytes in the database bloat it; a storage SDK in the handlers ties them to one vendor.

## Decision

`IFileStorage` (put, open, delete) with an S3-compatible adapter (S3, MinIO) and a local-disk adapter, chosen by `Storage:Provider`. Keys are built from ids the application owns. A handler writes the file first and then the row that holds its key. Size and content type are validated.

## What varies

The provider, the bucket, the limits.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Bytes in the database | Nothing extra | Large rows and backups |
| The vendor SDK in handlers | No port | Vendor lock-in; untestable handlers |
| **A port with two adapters** | One interface | n/a |

## Consequences

- Gained: handlers are testable with a fake; development needs no cloud; production can move between S3-compatible stores.
- Lost or owed: an orphan file when the row write fails; their cleanup is the project's own job.
- Cost of going back: low.

## How it is enforced

`LocalDiskFileStorageTests`, `S3FileStorageTests` against MinIO, the handler tests, and `storage.sh` (an upload reads back byte for byte).
