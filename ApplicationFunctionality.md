# Task Planner functionality

Task Planner is a Windows desktop task planning overlay. It runs as a tray-backed WPF application and keeps the main panel hidden until opened from the tray icon or the global keyboard shortcut.

## Tasks

- Users can quickly capture a task from the overlay.
- Users can view tasks by inbox, today, upcoming, and completed views.
- Users can edit a selected task's title and topic directly in the task list, without a separate details panel: each task row has an edit toggle that expands an inline editor beneath it.
- Priority, due date, and recurrence remain part of a task's data and are preserved when a task is edited, but they are not shown or editable in the current UI.
- Tasks display their position number in the current list.
- Web links in task titles are clickable from the task list and open in the user's default browser.
- Users can reorder tasks by dragging them with the mouse; the order is persisted.
- Priority can still be captured quickly with `!urgent`, `!high`, `!normal`, or `!low` when creating a task, even though it is not shown afterward.
- Users can complete or delete a task directly from its row in the task list.
- Users can create, rename, reorder, and delete customizable topics from the Manage topics controls. The built-in "No topic" entry cannot be renamed or deleted.
- Each task can be assigned to no topic or one topic.
- Inbox, today, upcoming, and completed views are displayed grouped by topic by default.
- The grouped list shows every user-defined topic as a section (including empty topics) followed by a "No topic" section, each listing its own tasks.
- Users can collapse or expand each topic section independently.
- Users can reorder user-defined topics by dragging their topic headers; the order is persisted. The "No topic" section remains last.
- Deleting a topic leaves its tasks intact and clears their topic assignment.
- Users can attach any number of free-text notes to a task from its inline editor, and add, edit, or delete each note independently without a separate save step.
- A truncated preview of a task's notes is shown directly in the task list overview, in smaller muted italic text below the task title.

## Panel behavior

- The panel can be shown or hidden with `Ctrl+Shift+Space`.
- The tray icon can open the panel or exit the application.
- The exit button at the right side of the panel header shuts down the application.
- The panel hides when Escape is pressed.
- The panel can automatically hide when it loses focus according to the user's panel settings.

## Appearance

- Users can choose the application theme from System, Light, and Dark.
- The selected theme is persisted between application launches.

## Startup behavior

- Users can toggle whether Task Planner starts automatically when they sign in to Windows.
- The startup preference is controlled from the main UI with a switch-style toggle.
- When enabled, Task Planner registers itself for the current Windows user only.
- When disabled, Task Planner removes its current-user startup registration.
- The startup preference is persisted and reflected in the UI on later launches.
