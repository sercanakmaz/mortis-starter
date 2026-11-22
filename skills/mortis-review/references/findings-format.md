# The report

Most severe first. One line of verdict, then the findings, then what was not checked.

```
Verdict: 2 errors, 1 warning in the change (12 files); 1 note outside it.

ERROR  src/Acme.Application/Orders/CompleteOrderCommand.cs:41  AGENTS 9 (decision 28)
       CompleteOrderCommandHandler also decreases the product's stock. Without it the order is still completed,
       so it is a consequence.
       Fix: raise nothing new (Completed exists); add DecreaseStockWhenOrderCompletedEventConsumer in
       Application/Products/ and remove the stock lines from the handler.

ERROR  src/Acme.Application/ApplicationModule.cs  AGENTS 13 (review.sh C05)
       ArchiveOrderCommandHandler is not registered.
       Fix: services.AddCommandHandler<ArchiveOrderCommand, OrderDto, ArchiveOrderCommandHandler>();

WARNING src/Acme.Domain/Services/PricingService.cs:5  decision 31 (review.sh C13)
       PricingService takes IProductRepository to call one finder.
       Fix: a read port IProductPriceReader in Domain/Ports/ with that one method.

NOTE (outside the change) test/Acme.Tests/Domain/Orders/OrderTests.cs:88  AGENTS 14
       A test asserts two unrelated behaviours.

Not checked: C11 (no --diff range given); dotnet test (no SDK in this environment).
```

- **Severity**: an error breaks a rule of `AGENTS.md` or a decision; a warning is a likely problem the rule names as a preference; a note is outside the change or a question.
- **Location**: the file and the line; a whole-file finding gives the file only.
- **Rule**: the `AGENTS.md` rule number, the decision, or the script check id.
- **Fix**: what to write, where, concretely enough to apply without asking. Name the new type and its folder.
- **Not checked**: every check the script did not run, every command that could not run, and why.
