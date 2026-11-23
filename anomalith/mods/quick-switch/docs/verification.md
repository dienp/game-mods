# Verification

## Live results

ScriptEngine loaded the updated plugin in the running game and retained the
saved 1.5× setting. The Other Settings toggle recorded both OFF and ON. The
live probe measured one active patch and 1.50× native animation speed after
changing the slider. For the same gun, the transition into aim-down-sights
took about 693 ms at 1.5× and 574 ms at 2.0×.

With the mod enabled, scrolling in either direction selected a different
weapon while the wheel stayed closed. The OFF branch calls the original
shortcut lookup. Reopening the stock wheel with the final build was checked
in code.

## IL2CPP build 26773190

The v1.0.8 plugin replaced the lookup result after the native lookup had
already opened the weapon wheel. The fix supplies the next weapon before
that lookup runs and lets the game finish the normal swap.

The fixed Debug DLL hot-reloaded at Front Entrance with BepInEx 6.0.0-be.793.
With shortcuts 1 and 4 assigned and the intervening slots empty, a single
notch switched from Grenade to Lightweight Assault Rifle and a notch in the
opposite direction switched back. The wheel stayed closed both times. Reloading
with the saved toggle OFF restored the stock wheel on the first notch. After
restoring ON and reloading again, the next notch switched directly. The saved
1.5× multiplier was retained. Swap animation timing was not measured in this run.

## Repeat the checks

In the shooting range, switch between the same two loaded guns while holding
aim. Compare the duration from the start of the native swap until it enters
the aim-down-sights action. Repeat at 1.0× and 3.0× with Quick Weapon Switch
ON. Switch it OFF and confirm that the gun uses its stock swap speed even
with the slider set higher.

Scroll one notch each way and check that the first notch starts a swap
without opening the weapon wheel. Turn the toggle OFF and check that the
stock wheel returns.

Hot-reload the DLL with Other Settings open. Check for one Quick Weapon Switch
row and one Switch Speed slider. The slider should look like Music Volume
and show the saved multiplier. Check that adjacent rows remain selectable.
