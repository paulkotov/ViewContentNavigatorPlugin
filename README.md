# View Content Navigator — Revit 2022 Plugin

A navigator for all categories and elements within a Revit model. Clicking the ribbon button creates a **temporary 3D view** that displays all categories and elements, and opens a modeless WPF window with a tree structure:

```
Revit Category
└── Family (present in view)
└── Instance            ← optional ("Instances" toggle switch)
```

Each tree node features:

- **Checkbox** — hide/show elements in the active (navigator) view.
- **Color** — override graphics (projection + surface solid fill) in this view.

All changes are applied **on the fly** ("Auto" toggle switch) or by clicking the "Update" button. Features include `Hide`, `Show`, `Color`, and `Reset Color` buttons, a quick color palette, and a custom color picker (`…`). View settings (visibility + colors) and the temporary view itself can be saved.

## Technologies

- C# / .NET Framework 4.8 (x64) — aligned with Revit 2022–2024 standards.
- Revit API (`IExternalApplication` + `IExternalCommand`).
- WPF + **MVVM** without third-party MVVM frameworks.
