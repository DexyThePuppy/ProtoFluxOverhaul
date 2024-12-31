using FrooxEngine;
using FrooxEngine.ProtoFlux;
using Elements.Core;
using System;
using System.Collections.Generic;
using static ProtoFluxVisualsOverhaul.Logger;

namespace ProtoFluxVisualsOverhaul
{
    /// <summary>
    /// Handles wire-related sounds for ProtoFlux
    /// </summary>
    public static class ProtoFluxSounds
    {
        // Cache for shared audio clips and their components
        private static readonly Dictionary<string, StaticAudioClip> sharedAudioClips = new Dictionary<string, StaticAudioClip>();
        private static readonly Dictionary<string, AudioClipPlayer> persistentPlayers = new Dictionary<string, AudioClipPlayer>();
        private static readonly Dictionary<string, AssetLoader<AudioClip>> assetLoaders = new Dictionary<string, AssetLoader<AudioClip>>();
        private static readonly Dictionary<string, Slot> activeAudioSlots = new Dictionary<string, Slot>();
        
        // List of all sound names we need to preload
        public static readonly string[] SOUND_NAMES = { "Connect", "Delete", "Grab" };
        
        // Track initialization
        public static bool isInitialized = false;
        public static World currentWorld;

        public static void Initialize(World world)
        {
            if (isInitialized) return;
            currentWorld = world;
            isInitialized = true;
        }

        /// <summary>
        /// Gets the appropriate sound URL from config based on the sound name
        /// </summary>
        public static Uri GetSoundUrl(string soundName)
        {
            Uri result;
            try
            {
                switch (soundName)
                {
                    case "Connect":
                        result = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.CONNECT_SOUND);
                        Logger.LogAudio("URL Config", $"Connect sound URL: {result}");
                        return result;
                    case "Delete":
                        result = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.DELETE_SOUND);
                        Logger.LogAudio("URL Config", $"Delete sound URL: {result}");
                        return result;
                    case "Grab":
                        result = ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.GRAB_SOUND);
                        Logger.LogAudio("URL Config", $"Grab sound URL: {result}");
                        return result;
                    default:
                        throw new ArgumentException($"Unknown sound name: {soundName}");
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to get sound URL for {soundName}", e, LogCategory.Audio);
                throw;
            }
        }

        /// <summary>
        /// Preloads all audio clips to ensure they're ready when needed
        /// </summary>
        public static void PreloadAudioClips(World world)
        {
            if (!isInitialized) Initialize(world);

            Logger.LogAudio("Preload", "Starting audio preload");
            
            // Find or create the necessary slots
            var tempSlot = world.RootSlot.FindChild("__TEMP") ?? world.RootSlot.AddSlot("__TEMP", false);
            var audioRoot = tempSlot.FindChild($"{world.LocalUser.UserName}-Sounds-ProtoFluxVisualsOverhaul") 
                           ?? tempSlot.AddSlot($"{world.LocalUser.UserName}-Sounds-ProtoFluxVisualsOverhaul", false);

            // Ensure cleanup when user leaves
            audioRoot.GetComponentOrAttach<DestroyOnUserLeave>().TargetUser.Target = world.LocalUser;

            // Preload all sound clips
            foreach (var soundName in SOUND_NAMES)
            {
                Logger.LogAudio("Preload", $"Preloading {soundName} sound");
                
                // Create or find the clip slot
                var clipSlot = audioRoot.FindChild(soundName + "SoundLoader") 
                              ?? audioRoot.AddSlot(soundName + "SoundLoader", false);

                // Check if we need to create new components
                if (!sharedAudioClips.TryGetValue(soundName, out var clip) || clip.IsRemoved)
                {
                    // Create and configure the StaticAudioClip
                    clip = clipSlot.AttachComponent<StaticAudioClip>();
                    clip.URL.Value = GetSoundUrl(soundName);
                    sharedAudioClips[soundName] = clip;

                    // Create and configure the AssetLoader
                    var assetLoader = clipSlot.AttachComponent<AssetLoader<AudioClip>>();
                    assetLoader.Asset.Target = clip;
                    assetLoader.Asset.ListenToAssetUpdates = true;
                    assetLoaders[soundName] = assetLoader;

                    // Create and configure the persistent player
                    var persistentPlayer = clipSlot.AttachComponent<AudioClipPlayer>();
                    persistentPlayer.Clip.Target = clip;
                    persistentPlayer.Loop = true;  // Keep it playing to prevent unloading
                    persistentPlayers[soundName] = persistentPlayer;

                    // Create a silent audio output to keep the clip loaded
                    var silentOutput = clipSlot.AttachComponent<AudioOutput>();
                    silentOutput.Volume.Value = 0f;  // Silent playback
                    silentOutput.Source.Target = persistentPlayer;

                    // Start playing when the asset is available
                    if (assetLoader.Asset.IsAssetAvailable)
                    {
                        persistentPlayer.Play();
                        Logger.LogAudio("Asset Load", $"Asset loaded immediately for {soundName}");
                    }
                    else
                    {
                        assetLoader.Asset.Changed += (IChangeable changeable) => {
                            if (assetLoader.Asset.IsAssetAvailable && !persistentPlayer.IsRemoved)
                            {
                                persistentPlayer.Play();
                                Logger.LogAudio("Asset Load", $"Asset loaded and playing for {soundName}");
                            }
                        };
                    }
                }
                else
                {
                    Logger.LogAudio("Cache", $"Using existing audio clip for {soundName}");
                    
                    // Ensure the persistent player is still playing
                    if (persistentPlayers.TryGetValue(soundName, out var player) && !player.IsRemoved && !player.IsPlaying)
                    {
                        player.Play();
                    }
                }
            }

            Logger.LogAudio("Preload", "Audio preload complete!");
        }

        public static StaticAudioClip GetOrCreateSharedAudioClip(string soundName)
        {
            // Check if we already have this clip
            if (sharedAudioClips.TryGetValue(soundName, out var existingClip))
            {
                if (!existingClip.IsRemoved) {
                    Logger.LogAudio("Cache", $"Using cached audio clip for {soundName}");
                    return existingClip;
                }
                Logger.LogAudio("Cache", $"Removing destroyed audio clip for {soundName}");
                sharedAudioClips.Remove(soundName);
            }

            Logger.LogAudio("Creation", $"Creating new audio clip for {soundName}");
            
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

        public static void PlaySoundAndCleanup(World world, float3 position, string soundName)
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

            Logger.LogAudio("Creation", $"Creating audio slot for {soundName}");

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
                Logger.LogAudio("Cleanup", $"Cleaning up audio slot for {soundName}");
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
            Logger.LogAudio("Playback", $"Started playing {soundName}");
        }

        public static void OnWireConnected(World world, float3 position)
        {
            if (!isInitialized) Initialize(world);
            if (!ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.WIRE_SOUNDS)) return;
            Logger.LogWire("Connect", "Playing connect sound");
            PlaySoundAndCleanup(world, position, "Connect");
        }

        public static void OnWireDeleted(World world, float3 position)
        {
            if (!isInitialized) Initialize(world);
            if (!ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.WIRE_SOUNDS)) return;
            Logger.LogWire("Delete", "Playing delete sound");
            PlaySoundAndCleanup(world, position, "Delete");
        }

        public static void OnWireGrabbed(World world, float3 position)
        {
            if (!isInitialized) Initialize(world);
            if (!ProtoFluxVisualsOverhaul.Config.GetValue(ProtoFluxVisualsOverhaul.WIRE_SOUNDS)) return;
            Logger.LogWire("Grab", "Playing grab sound");
            PlaySoundAndCleanup(world, position, "Grab");
        }
    }
} 