using UnityEngine;

namespace AMEAccess.Game
{
    /// <summary>How a tile is reported as you move over or past it.</summary>
    internal enum Feedback { Speech, Sound, Both }

    /// <summary>
    /// Tiles described by sound instead of words, using the game's own audio.
    ///
    /// Every sound here belongs to the game: a splash for water, a footstep for land, a tap on
    /// wood for a tree, the postbox tap for a postbox, the caption chime for an exhibit. Nothing
    /// is shipped, nothing is licensed, and it matches the game because it IS the game.
    ///
    /// They are also played AT the tile, so the sound arrives from the right direction. Speech
    /// has to say "3 north, 1 east"; a sound simply comes from there.
    ///
    /// This is for sweeping terrain, which is what words are slowest at. Names, plaque text,
    /// menus and anything with identity rather than position stay spoken in every mode - a
    /// sound can say "exhibit", never which exhibit.
    /// </summary>
    internal static class Sfx
    {
        internal static Feedback Mode
        {
            get
            {
                switch ((Cfg.FeedbackMode.Value ?? "speech").Trim().ToLowerInvariant())
                {
                    case "sound": return Feedback.Sound;
                    case "both": return Feedback.Both;
                    default: return Feedback.Speech;
                }
            }
        }

        internal static bool SoundOn => Mode != Feedback.Speech;
        internal static bool SpeechOn => Mode != Feedback.Sound;

        private static SoundLibrary Lib
        {
            get
            {
                var sim = Refs.Sim;
                return sim == null ? null : sim.sounds;
            }
        }

        /// <summary>
        /// Play the sound for whatever is on a tile.
        ///
        /// Returns the text that should still be spoken: the caller's own words in speech or
        /// both modes, and nothing at all in sound mode. Keeping that decision here means no
        /// caller has to know which mode is on.
        /// </summary>
        internal static string Tile(Vector3i pos, string spoken)
        {
            if (SoundOn) Play(pos);
            return SpeechOn ? spoken : "";
        }

        private static void Play(Vector3i pos)
        {
            var lib = Lib;
            if (lib == null) return;

            var piece = Survey.TopOfColumn(pos);
            var where = SoundPoint(pos, piece);

            // Water is the absence of a piece, which is why there is nothing to look up for it.
            if (piece == null) { Water(lib, where); return; }

            switch (piece.type)
            {
                case PieceType.Land:
                case PieceType.ShallowWater:
                case PieceType.Ramp:
                    Fire(lib.playerMove, where); return;          // a footstep

                case PieceType.Tree:
                    Fire(lib.treeTapStart, where); return;        // a tap on the trunk

                case PieceType.Log:
                    Fire(lib.rollLogOntoPlatformImpact, where); return;   // a wooden thunk

                case PieceType.TreeStump:
                    Fire(lib.playerChopImpact, where); return;

                case PieceType.Rock:
                case PieceType.Obstacle:
                    Fire(lib.playerKickImpact, where); return;    // stone

                case PieceType.Raft:
                    Fire(lib.formRaft, where); return;

                case PieceType.WarpPoint:
                    Fire(lib.tapPostbox, where); return;

                case PieceType.Campfire:
                case PieceType.Monument:
                    Fire(lib.hover, where); return;

                case PieceType.Villager:
                    Fire(lib.playerHug, where); return;

                case PieceType.Caption:
                    FireFlat(lib.viewCaption); return;            // NoOverlaps has no PlayAt

                case PieceType.Landmark:
                    PlayLandmark(lib, piece as Landmark, where); return;
            }

            Fire(lib.hover, where);
        }

        /// <summary>
        /// The splash.
        ///
        /// logFellIntoWater, the obvious choice, could barely be heard: it is the whole falling
        /// sequence, so it opens with the fall and only splashes later - and a tile sound that
        /// starts with near-silence is a tile sound you miss. The events ending in "Impact" are
        /// the moment of the hit itself, which is short, sharp and immediate.
        ///
        /// Several are tried in turn so that an unassigned one in the asset cannot leave water
        /// silent, which is the worst thing for it to be: water is the tile you most need to
        /// hear.
        /// </summary>
        private static void Water(SoundLibrary lib, Vector3 where)
        {
            // Named choices, because which of these sounds right is something only ears can
            // settle. Each is one event; if one of them sounds like two hits, that is the event
            // itself and the answer is to pick another rather than to change the code.
            switch ((Cfg.WaterSound.Value ?? "split").Trim().ToLowerInvariant())
            {
                case "fall": if (Try(lib.logFellIntoWater, where)) return; break;
                case "roll": if (Try(lib.rollLogIntoWaterImpact, where)) return; break;
                case "knock": if (Try(lib.knockOverLogIntoWaterImpact, where)) return; break;
                case "raft": if (Try(lib.moveRaftFallIntoWaterImpact, where)) return; break;
                case "standup": if (Try(lib.standUpLogIntoWater, where)) return; break;
                case "split": if (Try(lib.logSplitInWater, where)) return; break;
            }

            // Whatever was asked for could not play, so fall through the rest rather than let
            // water - the tile you most need to hear - go silent.
            if (Try(lib.logSplitInWater, where)) return;
            if (Try(lib.rollLogIntoWaterImpact, where)) return;
            if (Try(lib.moveRaftFallIntoWaterImpact, where)) return;
            if (Try(lib.knockOverLogIntoWaterImpact, where)) return;
            Try(lib.logFellIntoWater, where);
        }

        private static bool Try(Sounds.OneShot sound, Vector3 where)
        {
            if (sound == null) return false;
            Fire(sound, where);
            return true;
        }

        /// <summary>
        /// Landmarks are many different things, so they get many different sounds - the same
        /// split the spoken names use.
        /// </summary>
        private static void PlayLandmark(SoundLibrary lib, Landmark lm, Vector3 where)
        {
            switch (Landmarks.KindOf(lm))
            {
                case LandmarkKind.Exhibit: FireFlat(lib.viewCaption); return;
                case LandmarkKind.Friend: Fire(lib.playerHug, where); return;
                case LandmarkKind.Bench: Fire(lib.playerSitOnBench, where); return;
                case LandmarkKind.CoffeeHut: Fire(lib.playerPourCoffee, where); return;
                case LandmarkKind.PopcornHut: Fire(lib.playerGrabPopcorn, where); return;
                case LandmarkKind.Trophy: Fire(lib.forwardSelect, where); return;
                case LandmarkKind.Ferry: Fire(lib.playerJumpOffBoat, where); return;
                default: Fire(lib.playerKickImpact, where); return;   // a solid prop
            }
        }

        /// <summary>A move that could not happen.</summary>
        internal static void Blocked()
        {
            var lib = Lib;
            if (lib != null && SoundOn) Fire(lib.backDeselect, Vector3.zero, false);
        }

        private static void Fire(Sounds.OneShot sound, Vector3 where, bool positional = true)
        {
            if (sound == null) return;
            try
            {
                // Flat is the loudest a sound can be, with no direction at all.
                if (positional && Cfg.SoundDistance.Value > 0f) sound.PlayAt(where);
                else sound.Play();
            }
            catch { }
        }

        private static void FireFlat(Sounds.NoOverlaps sound)
        {
            if (sound == null) return;
            try { sound.Play(); } catch { }
        }

        /// <summary>
        /// Where to put the sound so it is both audible and directional.
        ///
        /// Playing it at the tile is the obvious thing and it is too quiet: the listener rides
        /// with the camera, which sits well back from an isometric board, so a tile a few
        /// squares away is far enough for the engine to fade it under the music and ambience.
        ///
        /// Distance is not the information we want anyway - direction is. So the sound is
        /// placed close to the listener, offset in the direction of the tile AS SEEN FROM YOUR
        /// MONSTER. East still sounds east, and it is near enough to be heard, because the
        /// distance in the audio no longer stands for the distance on the board.
        ///
        /// SoundDistance of zero turns the placement off entirely and plays flat, which is the
        /// loudest it can be.
        /// </summary>
        private static Vector3 SoundPoint(Vector3i tile, Piece piece)
        {
            Vector3 atTile = TileWorld(tile, piece);

            float near = Cfg.SoundDistance.Value;
            if (near <= 0f) return atTile;              // caller plays flat instead

            var listener = Listener();
            if (listener == null) return atTile;

            Vector3 from = PlayerWorld();
            Vector3 offset = atTile - from;
            offset.y = 0f;                              // height is not a direction you can hear

            if (offset.sqrMagnitude < 0.01f) return listener.position;

            return listener.position + offset.normalized * near;
        }

        private static Transform _listener;

        private static Transform Listener()
        {
            if (_listener != null) return _listener;

            var cam = Camera.main;
            if (cam == null)
            {
                var any = Object.FindObjectOfType<Camera>();
                if (any != null) cam = any;
            }

            _listener = cam == null ? null : cam.transform;
            return _listener;
        }

        private static Vector3 PlayerWorld()
        {
            var move = Refs.PlayerMove;
            if (move != null)
            {
                try { if (move.transform != null) return move.transform.position; }
                catch { }
            }

            var p = Refs.PlayerPos;
            return new Vector3(p.x, p.y, p.z);
        }

        private static Vector3 TileWorld(Vector3i pos, Piece piece)
        {
            if (piece != null)
            {
                try { if (piece.transform != null) return piece.transform.position; }
                catch { }
            }
            return new Vector3(pos.x, pos.y, pos.z);
        }

        internal static void ForgetListener() { _listener = null; }

        internal static string Describe()
        {
            switch (Mode)
            {
                case Feedback.Sound: return "Sound only. Tiles are reported by sound, not words.";
                case Feedback.Both: return "Sound and speech together.";
                default: return "Speech only.";
            }
        }

        /// <summary>Step through the three modes.</summary>
        internal static string Cycle()
        {
            switch (Mode)
            {
                case Feedback.Speech: Cfg.FeedbackMode.Value = "sound"; break;
                case Feedback.Sound: Cfg.FeedbackMode.Value = "both"; break;
                default: Cfg.FeedbackMode.Value = "speech"; break;
            }
            Cfg.Save();
            return Describe();
        }
    }
}
