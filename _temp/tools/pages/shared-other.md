# Shared: failures, persistence contracts and registration, C#

Project `{Name}.Shared`, folders `Exceptions/` and `Persistence/`, and `SharedModule.cs` at the project root.

## The failure family

Folder `Exceptions/`, namespace `{Name}.Shared.Exceptions`, one file per type, each needs `using System.Net;`. Every failure a user can see is an `ExceptionBase`: a stable code, an HTTP status, a message. The host turns it into `{ code, message, errors }`.

@@decl src/Boilerplate.Shared/Exceptions/ExceptionBase.cs::ExceptionBase@@

@@member src/Boilerplate.Shared/Exceptions/ExceptionBase.cs::ExceptionBase::Code@@

@@member src/Boilerplate.Shared/Exceptions/ExceptionBase.cs::ExceptionBase::HttpStatusCode@@

The subclasses are one declaration each, written exactly so:

@@decl src/Boilerplate.Shared/Exceptions/BusinessRuleException.cs::BusinessRuleException@@

@@decl src/Boilerplate.Shared/Exceptions/NotFoundException.cs::NotFoundException@@

@@decl src/Boilerplate.Shared/Exceptions/DuplicateException.cs::DuplicateException@@

@@decl src/Boilerplate.Shared/Exceptions/InvalidParameterException.cs::InvalidParameterException@@

@@decl src/Boilerplate.Shared/Exceptions/ReferenceViolationException.cs::ReferenceViolationException@@

@@decl src/Boilerplate.Shared/Exceptions/ForbiddenException.cs::ForbiddenException@@

@@decl src/Boilerplate.Shared/Exceptions/TransactionRequiredException.cs::TransactionRequiredException@@

@@decl src/Boilerplate.Shared/Exceptions/ValidationFailedException.cs::ValidationFailedException@@

@@member src/Boilerplate.Shared/Exceptions/ValidationFailedException.cs::ValidationFailedException::Errors@@

| Type | Code | Status | Raised by |
|---|---|---|---|
| `BusinessRuleException` | the rule's own code from `{Aggregate}Constants` | 409 | an aggregate operation |
| `NotFoundException` | `not_found` | 404 | a handler that loads by id |
| `DuplicateException` | `duplicate` | 409 | a unique index, through `DatabaseExceptionHandler` |
| `InvalidParameterException` | `invalid_parameter` | 400 | a value object or aggregate constructor; a check constraint |
| `ReferenceViolationException` | `reference_violation` | 400 | a foreign key, through `DatabaseExceptionHandler` |
| `ForbiddenException` | `forbidden` | 403 | an authorisation rule |
| `TransactionRequiredException` | `transaction_required` | 500 | a repository write outside a transaction |
| `ValidationFailedException` | `validation_failed` | 422 | the pipeline, from validators |

## Persistence contracts

Folder `Persistence/`, namespace `{Name}.Shared.Persistence`. Implemented in Infrastructure (`infrastructure-postgres.md`).

@@decl src/Boilerplate.Shared/Persistence/IRepository.cs::IRepository@@

@@member src/Boilerplate.Shared/Persistence/IRepository.cs::IRepository::CreateAsync@@

@@member src/Boilerplate.Shared/Persistence/IRepository.cs::IRepository::UpdateAsync@@

@@member src/Boilerplate.Shared/Persistence/IRepository.cs::IRepository::DeleteAsync@@

@@member src/Boilerplate.Shared/Persistence/IRepository.cs::IRepository::FindByIdAsync@@

@@member src/Boilerplate.Shared/Persistence/IRepository.cs::IRepository::FindByIdTrackingAsync@@

@@decl src/Boilerplate.Shared/Persistence/IUnitOfWork.cs::IUnitOfWork@@

@@member src/Boilerplate.Shared/Persistence/IUnitOfWork.cs::IUnitOfWork::HasTransaction@@

@@member src/Boilerplate.Shared/Persistence/IUnitOfWork.cs::IUnitOfWork::SaveChangesAsync@@

@@member src/Boilerplate.Shared/Persistence/IUnitOfWork.cs::IUnitOfWork::BeginTransactionAsync@@

@@member src/Boilerplate.Shared/Persistence/IUnitOfWork.cs::IUnitOfWork::CommitTransactionAsync@@

@@member src/Boilerplate.Shared/Persistence/IUnitOfWork.cs::IUnitOfWork::RollbackTransactionAsync@@

@@member src/Boilerplate.Shared/Persistence/IUnitOfWork.cs::IUnitOfWork::OnCommitted@@

- `FindByIdAsync` reads without tracking; `FindByIdTrackingAsync` is the read you change and save.

## SharedModule

File `SharedModule.cs`, namespace `{Name}.Shared`:

@@decl src/Boilerplate.Shared/SharedModule.cs::SharedModule@@

@@member src/Boilerplate.Shared/SharedModule.cs::SharedModule::AddSharedModule@@

- Registration is explicit: one line per service. Assembly scanning is never used.

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

@@usings shared-other.md@@
