lobby-character-preview-panel-header = Character
lobby-character-preview-panel-character-setup-button = Customize
lobby-character-preview-panel-unloaded-preferences-label = Your character preferences have not yet loaded, please stand by.
lobby-character-preview-prev-char-tooltip = Previous character
lobby-character-preview-next-char-tooltip = Next character
lobby-character-preview-ignore-allegiance = Ignore Allegiance
lobby-character-preview-ignore-allegiance-tooltip = When enabled, spawns your currently selected character regardless of allegiance matching.
# The toggle states below spell out on/off in the label itself rather than relying on the button's
# colour alone - color-only state indicators are hard to read at a glance and unreliable for anyone
# with a colour vision deficiency.
lobby-character-preview-ignore-allegiance-off = Ignore Allegiance: Off
lobby-character-preview-ignore-allegiance-on = Ignore Allegiance: On

# Two-line character summary shown beside the preview sprite. The pronoun and its verb have to stay
# inside one selector ("He is" vs "They are"), so the colour wraps the whole phrase.
lobby-character-summary-name = This is [color=#FFFFFF]{$name}[/color]
lobby-character-summary-age = [color=#58CCFF]{$gender ->
    [male] He is
    [female] She is
    [epicene] They are
    *[other] It is
}[/color] [color=#FF7A7A]{$age}[/color] years old
