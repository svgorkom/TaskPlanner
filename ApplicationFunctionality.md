# Task Planner functionality

Task Planner is a Windows desktop task planning overlay. It runs as a tray-backed WPF application and keeps the main panel hidden until opened from the tray icon or the global keyboard shortcut.

## Tasks

- Users can quickly capture a task from the overlay.
- Users can view tasks by inbox, today, upcoming, and completed views.
- Users can edit a selected task's title, priority, due date, and recurrence.
- Tasks display their position number in the current list.
- Web links in task titles are clickable from the task list and open in the user's default browser.
- Users can reorder tasks by dragging them with the mouse; the order is persisted.
- Priority can be captured quickly with `!urgent`, `!high`, `!normal`, or `!low`, and is shown in task details and summaries.
- Users can complete or delete a selected task.
- Users can attach any number of free-text notes to a task, and add, edit, or delete each note independently without a separate save step.
- A truncated preview of a task's notes is shown directly in the task list overview, in smaller muted italic text below the task summary.

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
