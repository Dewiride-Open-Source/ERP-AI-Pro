# Approvals

In one paragraph: a module declares each kind of approval it needs (ordered steps, each decided by the asker's manager or by a built-in role, and the working days each step has); it submits a request inside the command that changes its record, so the record and its request commit together; the request waits for each step in turn on `/platform/approvals`, where the person it waits for (or any administrator, in their place) approves it, sends it back or rejects it, and the asker may withdraw it while it waits; each decision runs in one transaction on the module's own context together with the module's outcome, so the record moves with the request or nothing changes ([ADR-0068](../adr/0068-approvals-requests-in-the-asking-modules-transaction-ordered-steps-by-manager-or-role-decided-with-the-modules-outcome-and-one-inbox.md)).

| Part | Where | What it does |
|---|---|---|
| Building block | `backend/BuildingBlocks/Approvals` (`AddErpApprovals()`, called by `AddErpPlatform` after `AddErpNumbering()` for every host) | the catalogue of kinds, `IApprovals` (submit, withdraw, status), `IApprovalDecisions`, `IApprovalInbox`, `IApprovalAdministration`, the port `IReportingManagers`, the routing and the working days, `ApprovalsDbContext` (schema `platform_approvals`) |
| Module | `backend/Modules/Platform/Approvals` (schema `null`, flag `Erp.Modules.Platform.Approvals`) | the routes under `/api/platform/approvals`, their handlers, and `UsersReportingManagers`, which answers `IReportingManagers` from `Identity.Users.Contracts.IReportingLines` |
| Identity contract | `Identity.Users.Contracts` `Positions/IReportingLines` | a person's managers on a date, nearest first, from their positions (ADR-0042) |
| Web | `frontend/apps/web/src/features/platform/approvals`, routes `/platform/approvals` (waiting for me), `/mine`, `/decided`, `/kinds`, `/all` and `/requests/[requestId]` | the lists, the request with its steps, the decision and withdrawal dialogs, the kinds of request with their settings |

A consumer references `BuildingBlocks.Approvals` alone, never `Platform/Approvals`.

## Declaring a kind

```csharp
builder.Services.AddApprovalKind<ExpensesDbContext, BillApprovalOutcomes>(new ApprovalKind(
    Key: "finance.expenses.bill-approval",
    Title: "Bill approval",
    Description: "A vendor bill waits for approval before it is posted to the books.",
    SubjectType: "finance.expenses.bills",
    Steps: [ApprovalStepDefinition.Manager("Manager"), ApprovalStepDefinition.HeldBy("Finance", ErpRole.Finance)],
    WorkingDaysToDecide: 2));
```

The key is three lower-case kebab segments under the module's permission prefix; the subject type is the change key of the record the kind approves; one to five steps with distinct titles, a role step naming `Finance`, `Hr`, `Manager` or `Administrator`; 1 to 30 working days. A malformed or duplicate kind fails when the host is built. `TContext` is the module's own context, which maps the lent tables (`public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();` and `public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();`), and `THandler` a sealed `IApprovalOutcomeHandler` in the module's `Integration` namespace.

## Submitting, withdrawing and the status

- `IApprovals.SubmitAsync(context, kind, subjectId, requestedFor, cancellationToken)` is called last in the command that changes the record, after its own checks, on the context that maps the tables, inside its unit of work. It plans every step (3 below), adds the request and its steps, and answers the request's id and state; while an administrator has switched the kind off it answers approved without a request, and the module applies the approval itself in the same command. A second waiting request for the same kind and record is refused (409 `approval.already-submitted`; the filtered unique index refuses one at the same moment as 409 `concurrency.conflict`).
- `IApprovals.WithdrawAsync(context, kind, subjectId, cancellationToken)` closes the record's waiting request without calling the outcome handler, because the module's command already moves the record. It is the first thing that command does, before it reads its record or takes a lock of its own; withdrawing and submitting again in one unit of work is refused, and neither call may run while a decision runs.
- `IApprovals.StatusOfAsync(context, kind, subjectId, cancellationToken)` answers the record's latest request (its id, state, the waiting step and its due time), or null.

## The outcome

```csharp
public sealed class BillApprovalOutcomes(ICommandHandler<ApplyBillApproval, Unit> apply) : IApprovalOutcomeHandler
{
    public Task<Result> HandleAsync(ApprovalOutcome outcome, CancellationToken cancellationToken) => ...;
}
```

When a request reaches a final state on the Approvals page, the block calls the kind's handler inside the decision's transaction on the module's context; the handler runs the module's own command through the pipeline, whose unit of work joins that transaction, and never saves or calls an outside service. A failed `Result` rolls the decision back and is the decider's answer. A handler may refuse `Approved`; it must accept `Returned`, `Rejected` and `Withdrawn`, or a request could be neither decided nor withdrawn. The command checks none of the decider's permissions: whether they may decide is the block's.

## Who decides

- A manager step goes to the asker's manager on the day of submission, walking up past a manager who is deactivated, has no account or submitted the request, and to the administrators when nobody is left. A role step goes to every holder of the role, checked against the decider's roles when they decide.
- Nobody decides a request they asked for or submitted, administrators included (403 `approval.own-request`). Any holder of `platform.approvals.requests.decide-any` (administrators) decides any other waiting request in place of its approver, and the step records that. One person may decide two steps of a request.
- Approving moves the request to its next step or approves it; sending back and rejecting need a reason of 1 to 1,000 characters and end the request; every final state is final, and asking again after changes is a new request, the record's earlier requests listed on its page.
- A decision takes the application lock `platform_approvals.<request id>` of its transaction and checks the request's state and the version the decider saw (409 `approval.not-waiting`, `concurrency.conflict`), so ten people deciding at once leave one decision.

## Time

Each step has its kind's working days, captured when the request is submitted; it is due at midnight India time at the end of its last working day, Monday to Friday, public holidays counting as working days until a holiday calendar exists. Lateness is read from the due time and shown (late requests first, marked Late); no job runs, and the daily reminder comes with `business-building-blocks-minimal-transactional-email-sender`.

## The page and who sees what

`/platform/approvals` lists what waits for the caller (late first), `/mine` what they asked for or submitted, `/decided` what they decided, `/kinds` each kind with its steps and setting (administrators switch a kind off and change its working days there), and `/all`, for administrators, every request. A request's page is visible to its asker, its submitter, anyone who decided one of its steps and anyone its waiting step waits for; anyone else is answered as for an unknown request. With the module's flag off, consumers still submit and nobody can decide until it is on again; switching a kind off also removes its maker-checker.

## Records

Requests, steps and kind settings are recorded under `platform.approvals.requests`, `platform.approvals.steps` (filed under their request) and `platform.approvals.kind-settings`; the person who decided is the actor of the request's and the record's change records under one correlation id. Nothing is deleted, and no integration event is published.

## Tests

The building block's unit tests cover the kinds, the state machine, the routing and the working days; its integration tests a sample record (`SampleClaim` on the persistence sample's context) through real pipeline commands, ten deciders at once and the module-side rules. The architecture's `ApprovalTests` hold every catalogue context's mapping of the tables and every kind's context, handler and keys. The testing library declares `TestApprovalKinds` (subject type `test.approvals.records`, on `ApprovalsDbContext`), their outcome handlers and `ApprovalRequestSeeder`; the end-to-end host submits a test kind's request for the calling session's person at `POST /api/__test/approvals/requests` (Testing only; `subjectId` asks again for a record whose request closed, `submittedWorkingDaysAgo` back-dates one so it is late), and the Playwright specs take a kind per project, theme and scenario (`approvalKind`, next to `numberingKind`).
