# Opacity control for View Content Navigator

Date: 2026-09-10

## Goal

Allow setting surface opacity independently of color overrides on navigator tree nodes.

## UI

- In each tree row, next to the color swatch: a small toggle button (`%`).
- Click opens a `Popup` with a 0–100% slider and a live `%` label.
- 100% = fully opaque; maps to Revit `SetSurfaceTransparency(100 - opacity)`.

## Behavior

- Opacity is independent of color.
- Honors **Авто**: live push when on; deferred until **Обновить** when off.
- Slider binding uses a short delay to avoid flooding Revit while dragging.
- Reset color clears color only; opacity is preserved (overrides are merged).
- Default opacity is 100 for all nodes.

## Data / API

- `TreeNodeViewModel.Opacity` (int 0–100), cascades to children.
- `INodeChangeSink.RequestOpacity`.
- `NodeState` includes `Opacity`.
- `IViewContentService.SetOpacity`; `SetColor` / `ResetColor` / `ApplyAll` merge with existing transparency.
