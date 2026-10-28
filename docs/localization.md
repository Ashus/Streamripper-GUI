# Localization

## Catalogs and keys

Embedded catalogs live at
`src/StreamripperGui.Desktop/Localization/en.json` and `cs.json`.
Use semantic PascalCase keys describing the final UI meaning, for example
StationName, SaveProfile, OpmlPreviewSummary and RulesSavedInactive.
Do not generate hash-based keys or use source text as a XAML resource key.

Both catalogs must contain the same nonempty keys and the same composite-format
arguments. English is the fallback. Unknown keys remain visible as their key,
making missing resources detectable. Tests reject hash keys, duplicate English
entries and mismatched placeholders.

Static XAML uses `{loc:T StationName}`. The markup extension observes
Localizer.Language so existing controls update immediately.

## Messages and arguments

View-model-owned messages call `Localizer.Current.Message(key, arguments)`.
This produces a canonical English message identity; TranslateBinding renders
it in the currently selected language. The same stored message can therefore
change language without re-running the action.

Use `Format(key, arguments)` for a string that must be localized immediately,
such as a native picker title or an explicitly refreshed stream description.
Do not store an already translated error as the canonical view-model message.

TranslationCatalog is UI-independent. It maps known English domain/IO messages
and parameterized application messages at the presentation boundary.
Core and Infrastructure remain independent of Desktop resources. When changing
one of their user-visible diagnostics, update the matching resource template
and its localization tests. Native/OS diagnostics without a known application
template remain verbatim.

Arguments preserve user content. Only designated nested error positions and
known validation-option labels are translated recursively, with a depth limit.
Never apply TranslateBinding to a station/profile/filter name, URL, rule document
or raw Streamripper transcript. Localizer.Message does not translate its arguments.

New generated rule-template comments and developer logs are English. Existing
rule files/comments are user data and are not rewritten for language changes.

## Language selection

Settings persists `application.language` as `auto`, `en` or `cs`.
Automatic uses CurrentUICulture: Czech regional cultures select Czech; other
cultures use English. Persist the mode rather than the detected result.
Selection changes apply immediately and save to configuration.

Builtin Default/None labels update with language changes. Named user entries
remain unchanged. Application appearance is independent of language.

## Maintenance checks

Run `dotnet test -c Release`. LocalizationTests compiles the UI-independent
catalog with embedded production resources and verifies key/argument parity,
fallback, repeat rendering, nested diagnostics and preserved user/native text.

For UI review, switch EN/CZ on existing windows and check tabs, profile choices,
validation errors, import preview, deletion dialogs and picker labels.
A valid key does not prove that a new control uses it: review direct Text,
Content, Header, Title, PlaceholderText and tooltip literals when adding UI.
