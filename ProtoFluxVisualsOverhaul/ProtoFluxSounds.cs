using FrooxEngine;
using FrooxEngine.ProtoFlux;
using Elements.Core;
using System;
using System.Collections.Generic;

namespace ProtoFluxVisualsOverhaul
{
    /// <summary>
    /// Handles wire-related sounds for ProtoFlux
    /// </summary>
    public static class ProtoFluxSounds
    {
        // Cache for shared audio clips
        private static readonly Dictionary<string, StaticAudioClip> sharedAudioClips = new Dictionary<string, StaticAudioClip>();
        private static readonly Dictionary<string, Slot> activeAudioSlots = new Dictionary<string, Slot>();
        
        // List of all sound names we need to preload
        private static readonly string[] SOUND_NAMES = { "Connect", "Delete", "Grab" };
        
        // Track initialization
        private static bool isInitialized = false;
        private static World currentWorld;

        public static void Initialize(World world)
        {
            if (isInitialized) return;
            currentWorld = world;
            isInitialized = true;
        }

        /// <summary>
        /// Gets the appropriate sound URL from config based on the sound name
        /// </summary>
        private static Uri GetSoundUrl(string soundName)
        {
            Uri result;
            try
            {
                switch (soundName)
                {
                    case "Connect":
                        result = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.CONNECT_SOUND);
                        ProtoFluxVisualsOverhaul.Msg($"🎵 Connect sound URL: {result}");
                        return result;
                    case "Delete":
                        result = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.DELETE_SOUND);
                        ProtoFluxVisualsOverhaul.Msg($"🎵 Delete sound URL: {result}");
                        return result;
                    case "Grab":
                        result = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.GRAB_SOUND);
                        ProtoFluxVisualsOverhaul.Msg($"🎵 Grab sound URL: {result}");
                        return result;
                    default:
                        throw new ArgumentException($"Unknown sound name: {soundName}");
                }
            }
            catch (Exception e)
            {
                ProtoFluxVisualsOverhaul.Msg($"❌ Error getting sound URL for {soundName}: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Preloads all audio clips to ensure they're ready when needed
        /// </summary>
        public static void PreloadAudioClips(World world)
        {
            if (!isInitialized) Initialize(world);

            ProtoFluxVisualsOverhaul.Msg("🎵 Starting audio preload...");
            ProtoFluxVisualsOverhaul.Msg($"🔧 Config initialized: {ProtoFluxVisualsOverhaul.Config != null}");
            ProtoFluxVisualsOverhaul.Msg($"🔧 Wire sounds enabled: {ProtoFluxVisualsOverhaul.Config?.GetValue(ProtoFluxVisualsOverhaul.WIRE_SOUNDS)}");

            // Find existing __TEMP slot first
            var tempSlot = currentWorld.RootSlot.FindChild("__TEMP");
            var audioRoot = tempSlot?.FindChild($"{currentWorld.LocalUser.UserName}-Sounds-ProtoFluxVisualsOverhaul");

            // If we don't have the slots yet, create them
            if (tempSlot == null) {
                tempSlot = currentWorld.RootSlot.AddSlot("__TEMP", false);
            }
            if (audioRoot == null) {
                audioRoot = tempSlot.AddSlot($"{currentWorld.LocalUser.UserName}-Sounds-ProtoFluxVisualsOverhaul", false);
            }

            // Create a temporary slot for preloading
            var preloadSlot = audioRoot.AddSlot("PreloadSlot", false);

            // Preload all sound clips
            foreach (var soundName in SOUND_NAMES)
            {
                ProtoFluxVisualsOverhaul.Msg($"🎵 Preloading {soundName} sound...");
                
                // Look for existing clip slot first
                var clipSlot = audioRoot.FindChild(soundName + "SoundLoader");
                if (clipSlot == null) {
                    clipSlot = audioRoot.AddSlot(soundName + "SoundLoader", false);
                }
                
                // Only create new clip if needed
                StaticAudioClip clip;
                if (!sharedAudioClips.TryGetValue(soundName, out var existingClip) || existingClip.IsRemoved)
                {
                    // Check if the slot already has a StaticAudioClip
                    clip = clipSlot.GetComponent<StaticAudioClip>();
                    if (clip == null) {
                        clip = clipSlot.AttachComponent<StaticAudioClip>();
                        clip.URL.Value = GetSoundUrl(soundName);

                        // Add an AssetLoader to manage the audio clip and keep it loaded
                        var assetLoader = clipSlot.AttachComponent<AssetLoader<AudioClip>>();
                        assetLoader.Asset.Target = clip;
                        assetLoader.Asset.ListenToAssetUpdates = true;  // Listen for asset updates

                        // Create a persistent player to keep the asset loaded
                        var persistentPlayer = clipSlot.AttachComponent<AudioClipPlayer>();
                        persistentPlayer.Clip.Target = clip;
                        
                        // Add a silent audio output to keep the clip loaded
                        var silentOutput = clipSlot.AttachComponent<AudioOutput>();
                        silentOutput.Volume.Value = 0f;
                        silentOutput.Source.Target = persistentPlayer;
                        
                        // Play immediately if available, otherwise wait for the asset
                        if (assetLoader.Asset.IsAssetAvailable)
                        {
                            persistentPlayer.Play();
                            ProtoFluxVisualsOverhaul.Msg($"✅ Asset loaded immediately for {soundName}");
                        }
                        else
                        {
                            ProtoFluxVisualsOverhaul.Msg($"⏳ Waiting for asset to load for {soundName}");
                            assetLoader.Asset.Changed += (IChangeable changeable) => {
                                if (assetLoader.Asset.IsAssetAvailable && !persistentPlayer.IsRemoved)
                                {
                                    persistentPlayer.Play();
                                    ProtoFluxVisualsOverhaul.Msg($"✅ Asset loaded and playing for {soundName}");
                                }
                            };
                        }

                        // Ensure cleanup when user leaves
                        clipSlot.GetComponentOrAttach<DestroyOnUserLeave>().TargetUser.Target = currentWorld.LocalUser;
                    }

                    // Cache the clip
                    sharedAudioClips[soundName] = clip;
                    ProtoFluxVisualsOverhaul.Msg($"✅ Created new audio clip for {soundName}");
                }
                else
                {
                    clip = existingClip;
                    ProtoFluxVisualsOverhaul.Msg($"✅ Using existing audio clip for {soundName}");
                }
            }

            // Clean up preload slot
            preloadSlot.Destroy();

            ProtoFluxVisualsOverhaul.Msg("🎵 Audio preload complete!");
        }

        private static StaticAudioClip GetOrCreateSharedAudioClip(string soundName)
        {
            // Check if we already have this clip
            if (sharedAudioClips.TryGetValue(soundName, out var existingClip))
            {
                if (!existingClip.IsRemoved) {
                    ProtoFluxVisualsOverhaul.Msg($"🎵 Using cached audio clip for {soundName}");
                    return existingClip;
                }
                ProtoFluxVisualsOverhaul.Msg($"🗑️ Removing destroyed audio clip for {soundName}");
                sharedAudioClips.Remove(soundName);
            }

            ProtoFluxVisualsOverhaul.Msg($"🎵 Creating new audio clip for {soundName}");
            
            // Find existing __TEMP slot first
            var tempSlot = currentWorld.RootSlot.FindChild("__TEMP");
            var audioRoot = tempSlot?.FindChild($"{currentWorld.LocalUser.UserName}-Sounds-ProtoFluxVisualsOverhaul");

            // If we don't have the slots yet, create them
            if (tempSlot == null) {
                tempSlot = currentWorld.RootSlot.AddSlot("__TEMP", false);
            }
            if (audioRoot == null) {
                audioRoot = tempSlot.AddSlot($"{currentWorld.LocalUser.UserName}-Sounds-ProtoFluxVisualsOverhaul", false);
            }

            // Look for existing clip slot first
            var clipSlot = audioRoot.FindChild(soundName + "SoundLoader");
            if (clipSlot == null) {
                clipSlot = audioRoot.AddSlot(soundName + "SoundLoader", false);
            }

            // Check if the slot already has a StaticAudioClip
            var clip = clipSlot.GetComponent<StaticAudioClip>();
            if (clip == null) {
                clip = clipSlot.AttachComponent<StaticAudioClip>();
                clip.URL.Value = GetSoundUrl(soundName);

                // Ensure cleanup when user leaves
                clipSlot.GetComponentOrAttach<DestroyOnUserLeave>().TargetUser.Target = currentWorld.LocalUser;
            }

            // Cache the clip
            sharedAudioClips[soundName] = clip;
            return clip;
        }

        private static void PlaySoundAndCleanup(World world, float3 position, string soundName)
        {
            // Get or create the shared audio clip
            var clip = GetOrCreateSharedAudioClip(soundName);
            if (clip == null) return;

            // Clean up old audio slot if it exists
            if (activeAudioSlots.TryGetValue(soundName, out var oldSlot))
            {
                if (!oldSlot.IsDestroyed)
                {
                    oldSlot.Destroy();
                }
                activeAudioSlots.Remove(soundName);
            }

            ProtoFluxVisualsOverhaul.Msg($"🔊 Creating audio slot for {soundName}");

            // Create slot and disable it by default
            var slot = world.AddSlot($"Wire{soundName}Sound");
            slot.ActiveSelf = false;

            // Setup audio components
            var audioOutput = slot.AttachComponent<AudioOutput>();
            var audioClipPlayer = slot.AttachComponent<AudioClipPlayer>();

            // Configure audio output with config values
            audioOutput.Source.Target = audioClipPlayer;
            audioOutput.Spatialize.Value = true;
            audioOutput.MinDistance.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.MIN_DISTANCE);
            audioOutput.MaxDistance.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.MAX_DISTANCE);
            audioOutput.Volume.Value = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.AUDIO_VOLUME);

            // Configure audio player
            audioClipPlayer.Clip.Target = clip;
            slot.LocalPosition = position;

            // Add StoppedPlayableCleaner with a very short grace period
            var cleaner = slot.AttachComponent<StoppedPlayableCleaner>();
            cleaner.Playable.Target = audioClipPlayer;
            cleaner.GracePeriod.Value = 0.1f; // Very short grace period since we don't need to keep this instance
            cleaner.CheckingUser.Target = world.LocalUser;

            // Register for cleanup notification
            slot.OnPrepareDestroy += (s) => {
                ProtoFluxVisualsOverhaul.Msg($"🗑️ Cleaning up audio slot for {soundName}");
                if (activeAudioSlots.ContainsKey(soundName))
                {
                    activeAudioSlots.Remove(soundName);
                }
            };

            // Store the active audio slot
            activeAudioSlots[soundName] = slot;

            // Enable slot and play
            slot.ActiveSelf = true;
            audioClipPlayer.Play();
            ProtoFluxVisualsOverhaul.Msg($"▶️ Started playing {soundName}");
        }

        public static void OnWireConnected(World world, float3 position)
        {
            if (!isInitialized) Initialize(world);
            if (!ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.WIRE_SOUNDS)) return;
            ProtoFluxVisualsOverhaul.Msg("🔌 Wire connected, playing sound...");
            PlaySoundAndCleanup(world, position, "Connect");
        }

        public static void OnWireDeleted(World world, float3 position)
        {
            if (!isInitialized) Initialize(world);
            if (!ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.WIRE_SOUNDS)) return;
            ProtoFluxVisualsOverhaul.Msg("❌ Wire deleted, playing sound...");
            PlaySoundAndCleanup(world, position, "Delete");
        }

        public static void OnWireGrabbed(World world, float3 position)
        {
            if (!isInitialized) Initialize(world);
            if (!ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.WIRE_SOUNDS)) return;
            ProtoFluxVisualsOverhaul.Msg("✋ Wire grabbed, playing sound...");
            PlaySoundAndCleanup(world, position, "Grab");
        }
    }
} 