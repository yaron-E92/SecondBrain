# SecondBrain Core smoke journeys

Use this matrix before completing a Core UX change. It verifies that a person
can move from context to content and back; isolated unit-test success is not a
substitute for the applicable journey.

## Journey matrix

| ID | Starting state and human demo | Expected recovery path | Automated evidence | Manual evidence |
| --- | --- | --- | --- | --- |
| CORE-01 | Fresh install: open PARA, create the `Writing` Area, create the `Launch` Project, open `Launch`, and create `Launch brief` there. This is the foundational #100/#101 demo. | The new Area and Project appear without seeded data; `Launch` opens as an actionable workspace; the note is visible and can be opened; Back returns to PARA or the originating Home route. | `FreshInstall_CreatesContextsContentAndRelatedWorkspaces` | Android and Windows A-C |
| CORE-02 | From Home, quick-capture `Review the launch outline`; open Inbox, select the capture, edit its typed Idea title/content, move it to `Launch`, and find it from the Project. Continue by creating a Note or Resource from that workspace. This carries the existing quick-capture path into the typed-editor demo from #39. | Home refreshes Inbox immediately; Inbox has an open action; Save/Cancel returns to the originating screen; the moved item disappears from Inbox and appears in `Launch`. | `QuickCapture_CanBeEditedMovedArchivedRestoredAndRetrieved` plus `FreshInstall_CreatesContextsContentAndRelatedWorkspaces` | Android and Windows B-D |
| CORE-03 | Open `Writing`, create `Writing guide` from the Area workspace, link it to `Launch brief`, follow `Launch` under related workspaces, then go Back. This is the contextual workspace/relationship demo associated with #41. | The contextual Resource appears immediately; the related Project is discoverable; Back restores `Writing` and its items rather than dropping the user at an unrelated root. | `FreshInstall_CreatesContextsContentAndRelatedWorkspaces` | Android and Windows C-D |
| CORE-04 | Open `Launch outline review`, edit it, move it between Project and Area, archive it, open Archive, restore it, and retrieve it from its current workspace. | Each save refreshes visible collections and placement selectors; Archive removes the active item; Restore returns it to the same primary placement; the item remains openable and keeps its identity/content. | `QuickCapture_CanBeEditedMovedArchivedRestoredAndRetrieved` | Android and Windows C-D |
| CORE-05 | Start with no contexts/items, and separately attempt a load/save while storage fails. | Empty Home, Inbox, PARA, Project, and Area states expose capture/create/manage actions. Load failure keeps Retry visible. Validation or save failure keeps entered text editable and a second attempt can succeed. | `LoadAndSaveFailures_RetainUsefulStateAndCanBeRetried` and the two journey tests above | Android and Windows E-F |
| CORE-06 | Start in each shell entry: Home, Inbox, PARA, a Project, and an Area. Open the next useful object, then return. | Home and Inbox items open in Editor and return to their source; PARA opens a workspace; Project/Area open items and related contexts; Back/breadcrumbs preserve the originating workspace or route. | All `CoreSmokeJourneyTests` plus existing `ParaBrowserViewModelTests` workspace-history coverage | Android and Windows B-D |

The automated fixture creates every context and item required by the
fresh-install demos through public view-model/application behavior. Existing
data is exercised by continuing the same repository state through edit, move,
archive, restore, retry, and retrieval operations.

## Automated check

From the repository root, run:

```powershell
dotnet restore SecondBrain.slnx
dotnet build SecondBrain.slnx --configuration Debug --no-restore
dotnet test SecondBrain.slnx --configuration Debug --no-build --no-restore
```

The product-level tests are in
`tests/SecondBrain.Application.Tests/CoreSmokeJourneyTests.cs`. Platform UI
automation is N/A until the repository adopts a supported MAUI UI harness.

## Android and Windows manual smoke

Run the same checks on Android and Windows. For Android, use a touch device or
emulator and enable TalkBack for the accessibility pass. For Windows, use mouse
and keyboard and enable Narrator. Begin once with cleared app data (fresh
install) and once with the data produced by A-D (existing data).

### A. Fresh structure

1. Launch the app and open PARA. Confirm the Project, Area, and Resource Topic
   empty sections each include a creation button rather than dead text.
2. Create Area `Writing`. Create Project `Launch` with outcome
   `Ship the Core journey`. Confirm both appear immediately without restarting.
3. Open `Launch` from PARA and, separately, from Home's Current Projects.
   Confirm its breadcrumb/name is visible and Back returns to the originating
   PARA or Home route.

### B. Home and Inbox

1. On Home, enter `Review the launch outline`, activate **Save to Inbox**, and
   confirm the Home Inbox list refreshes immediately.
2. Open Inbox and activate the captured row. Confirm Editor opens the same title
   and content; edit both fields and Save. Confirm **Back to inbox** returns to
   Inbox and the changed row is visible.
3. Repeat by opening the item from Home Recent/Favorites when present. Confirm
   **Back to home** restores Home. With an empty Inbox, confirm **Go to quick
   capture** returns to Home.

### C. Project and Area workspaces

1. In `Launch`, activate **New Note**, save `Launch brief`, and confirm it appears
   under Notes. Open it, edit it, and use **Back to workspace**; confirm the
   `Launch` breadcrumb and item selection are available again.
2. In `Writing`, activate **New Resource**, save `Writing guide`, and confirm it
   appears under Resources. Move an item into and out of both `Writing` and
   `Launch`; confirm the source and destination lists refresh without restart.
3. Link the two items in PARA. From `Writing`, open the related `Launch`
   workspace, then Back. Confirm the prior `Writing` workspace is restored.

### D. Archive and navigation recovery

1. From a Project item, archive it and confirm it leaves the active workspace.
   Open PARA > Archive, restore it, then retrieve it from its original workspace.
2. Exercise Back after Editor, a related workspace, and an unavailable/archived
   workspace. Confirm no path strands the user and no created/changed result is
   lost.
3. On Android, also use the system Back gesture/button. On Windows, use the
   visible Back buttons and keyboard navigation. Confirm focus returns to a
   sensible control in the originating screen and the next Tab/swipe continues
   in reading order.

### E. Empty and failure states

1. On fresh data, visit Home, Inbox, PARA, `Launch`, and `Writing`. Confirm each
   empty state offers the relevant quick-capture, create, or manage action.
2. In a debug session, make the registered `ICoreKnowledgeRepository` throw once
   from `LoadStateAsync`. Confirm the visible error preserves the current screen,
   **Retry** is reachable, and retry loads the content after the fault is removed.
3. Make `SaveStateAsync` throw once while creating a context and while editing an
   item. Confirm entered values remain visible and editable; remove the fault,
   retry Save, and confirm the result appears in its collection.

### F. Accessibility, focus, and layout

1. Complete A-E once with touch on Android and once with mouse/keyboard on
   Windows. On Windows, repeat primary actions using Tab, Shift+Tab, Enter, and
   Space only. Focus order must follow the visible reading/action order.
2. With TalkBack/Narrator, traverse tabs, headings, text inputs, Retry, creation,
   item rows, and Back controls. Confirm each actionable control has a useful
   spoken name and state; labels must not be the only way to infer an action.
3. Enable the platform's largest text setting and use a narrow phone window or
   narrow Windows app window. Repeat quick capture, context creation, item edit,
   move, and Back. Text and actions may wrap or scroll but must not overlap,
   truncate required meaning, or become unreachable.
4. File and notification behavior is N/A for these Core journeys; record N/A
   unless the PR adds such platform behavior, in which case add its evidence to
   the affected journey row.

## Pull-request evidence

Relevant Core UX pull requests must include:

- journey IDs exercised (for example, `CORE-01`, `CORE-03`);
- the restore/build/test result and the `CoreSmokeJourneyTests` result;
- Android evidence for the applicable manual letters;
- Windows evidence for the applicable manual letters;
- any platform N/A with a reason; and
- any failed row as an explicit blocker rather than marking the PR ready.
