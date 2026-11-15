# Little Echoes v4.2

App 4.2.0, build 35, package `com.littleechoes.app`, for PICO OS 6 in PICO XR mode (SDK 6.1.1) with Unity 6000.3.25f1.
A design refinement on top of v4.1: the game now looks and sounds made for teenagers and adults who want to let their
inner kid out, rather than for small children. Every change was chosen by the owner from rendered options.

## What changed
- **A meadow that follows the clock.** From 6am to 6pm the lesson's meadow is in warm golden-hour light; from 6pm to
  6am it is at dusk, under a coral-to-indigo sky. The game reads the headset's clock as it starts.
- **A finished sky.** One smooth gradient, a sun by day, a crescent moon and stars at night. The sun and moon sit
  where the scene's light comes from.
- **Round cumulus clouds,** round from every side.
- **A hanging title sign.** The title card is a deep plum plate that hangs from the wooden frame on two steel rings.
  A soft gust rocks it every so often, and it swings when you push it with a hand or a controller.
- **Start for hands.** With controllers, Start is a green button. With bare hands it becomes a dashed green handprint,
  tilted like a hand raised for a high five: look and pinch, or put your palm on it. The sign stands at arm's
  reach from the first frame and never moves toward the player, so nobody has to step forward to touch it; the
  ball's cloud waits beside it.
- **The three friends stay hidden** in the bush until they hop out onto the leaf, from any angle.
- **Terracotta furniture** (burnt-orange chair, dark wood table, tan lamp shade) and **navy menu plates** on the
  green leaf.
- **A calmer countdown.** 3, 2, 1 in the title's yellow, GO! THROW! in green; the speech bubble and the "Sure?" card
  match the title.
- **Painted light.** Shapes have a lit side and a shaded side, with a small crisp highlight; characters and furniture
  catch a warm rim of light; the far hills fade into the sky.
- **Wind.** Grass, flowers, bushes and trees sway gently.
- **Lower, softer interface sounds:** the same notes an octave down, and shorter.
- **What you aim at lights up.** The button, tile, character or ball under the controller's ray or your gaze is
  drawn a little lighter until the pointer leaves it, as well as the gold ray, the dot and the hover tick.
- **Controller vibration:** a tick when a press lands, a pulse as a ball is picked up and thrown, and a thump when
  your note sounds, stronger for heavier balls. Controllers only. There is no menu switch for it yet.

## Evidence
- Unity tests: EditMode 256 of 256, PlayMode 114 of 114.
- On a PICO headset (build 32, the owner playing, 2026-11-14): 85 to 90 fps with the painted light, haze and wind
  (session log, 10-second averages).
- Rendered in Unity's editor renderer (not the headset) for every change.

## Not yet verified on a headset
- Builds 33 to 35's additions: the sky, the vibration, the sign standing at arm's reach, the friends hidden in the
  bush, the light on what you aim at.
- How the sign's swing, the push and the handprint feel with real hands.
- The new interface sounds have been measured (pitch, level) but not listened to.

## Known limits
- **Echo Battle** (the hidden two-headset mode) hasn't been tried on two headsets.
- **Instrument sounds** are original synthesis, not recordings of the traditional instruments.
