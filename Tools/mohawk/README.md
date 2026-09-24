# Mohawk landing audio

The ten-second arrival cue starts at 0:06 of Vospi's **super specific landing**,
then crossfades for 250 ms into **Creaky Thunderous SciFi SpaceShip Landing** by
Kalpesh Ajugia (kalsstockmedia) at approximately 1.9 seconds. It plays both aboard
the dropship and at the landing zone. The output is mono Ogg Vorbis at 48 kHz.

Source links, licenses, exact cuts and hashes are recorded in `landing_audio.json`.
With numpy, soundfile and ffmpeg (or imageio-ffmpeg) installed, rebuild using:

```sh
python Tools/mohawk/build_landing_audio.py path/to/super-specific-landing.mp3 path/to/creaky-thunderous.mp3
```

Keep the original downloads outside the repository. The edited cue's combined
license and author credits are in the audio directory's `attributions.yml`.

`MohawkLandingAudioTest` checks the arrival timing, playback at both locations,
cleanup at touchdown, and unchanged stock dropship playback.
