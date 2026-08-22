# Heading for the lobby action panel once the round is running. Upstream blanks the countdown at
# that point, which left the panel's heading slot empty and the button grid hard against the top
# border; these give the panel the same two-line head in both states.
cmu-lobby-state-round-in-progress = Round in progress
cmu-lobby-state-round-elapsed = Round time: {$hours}h {$minutes}m

# The faction choices live in a popup rather than on the lobby panel itself.
cmu-lobby-join-round = Join the Round
cmu-lobby-join-round-window-title = Join the Round
cmu-lobby-join-round-prompt = Pick a side to join as.

# PLACEHOLDER TEXT - lorem ipsum standing in until the real descriptions are written.
# One per choice in the join popup, kept to two or three sentences so the list stays scannable.
cmu-lobby-join-colonists-desc = Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.
cmu-lobby-join-govfor-desc = Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris. Nisi ut aliquip ex ea commodo consequat duis aute irure.
cmu-lobby-join-opfor-desc = Duis aute irure dolor in reprehenderit in voluptate velit esse cillum. Excepteur sint occaecat cupidatat non proident sunt in culpa.
cmu-lobby-join-other-desc = Sunt in culpa qui officia deserunt mollit anim id est laborum. Sed ut perspiciatis unde omnis iste natus error sit voluptatem.

# The ready toggle names the state it is IN. The slashes are doing the same job as the inverted
# fill behind them - the state has to be obvious without reading the word. Only the ready side is
# marked, deliberately: an empty bracket on the other one added a second thing to read without
# saying anything the dark fill and the dim text did not already say.
cmu-lobby-ready-yes = /// READY ///
cmu-lobby-ready-no = Not ready

# The round clock: a caption saying what is being counted, and a face holding only the value. The
# split is what lets the face be sized to be read across the screen - a face carrying a whole
# sentence could not be.
cmu-lobby-clock-caption-countdown = Round starts in
cmu-lobby-clock-caption-start = Round start
cmu-lobby-clock-caption-elapsed = Round in progress
cmu-lobby-clock-face-soon = Soon
cmu-lobby-clock-face-paused = Paused
cmu-lobby-clock-face-now = Now
