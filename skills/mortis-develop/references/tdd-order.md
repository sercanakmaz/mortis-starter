# The order of work

Every recipe step is written in this order. The point is that each test is seen failing for the right reason before the code that makes it pass exists.

1. **Test support first.** The fake (`Fake{Aggregate}`, a subclass with `Create()` and `With...()` that call the real operations) and the builder (`{Port}Builder`) the test will need, in `TestSupport/`, mirrored like `src/`.
2. **The failing test.** One test class per type, named after it; only `[Fact]` and `[Theory]` methods. Run it and see it fail: a compile error for a type that does not exist yet counts, an assertion failure is better.
3. **The smallest code that passes.** In the file the recipe row names, following the page it cites.
4. **Green.** The test passes unchanged. If it does not, fix the code, never the test: narrowing a scenario or fitting the expected value to the output is not allowed.
5. **Build and the whole suite.** `dotnet build` with 0 warnings, `dotnet test` green, before the next row.

For an aggregate this gives: aggregate test → aggregate (and its events) → configuration test → configuration and repository → handler test → command and handler → registration → controller → migration → `verify.sh`.

A bug fix starts at step 2 with a test that fails with the bug's own symptom, and ends with a row in `docs/incidents.md` naming that test.
