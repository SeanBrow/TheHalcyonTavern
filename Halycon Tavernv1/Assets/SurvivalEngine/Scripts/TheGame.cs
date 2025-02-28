﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace SurvivalEngine
{

    /// <summary>
    /// Game Manager Script for Survival Engine
    /// Author: Indie Marc (Marc-Antoine Desbiens)
    /// </summary>

    public class TheGame : MonoBehaviour
    {
        //non-static UnityActions only work in a game scene that uses TheGame.cs
        public UnityAction<string> beforeSave; //Right after calling Save(), before writing the file on disk
        public UnityAction<bool> onPause; //When pausing/unpausing the game
        public UnityAction onStartNewGame; //After creating a new game and after the game scene has been loaded, only first time if its a new game.

        //static UnityActions work in any scene (including Menu scenes that don't have TheGame.cs)
        public static UnityAction afterLoad; //Right after calling Load(), after loading the PlayerData but before changing scene
        public static UnityAction afterNewGame; //Right after calling NewGame(), after creating the PlayerData but before changing scene
        public static UnityAction<string> beforeChangeScene; //Right before changing scene (for any reason)

        private bool paused = false;
        private bool paused_by_player = false;
        private float death_timer = 0f;
        private float speed_multiplier = 1f;
        private bool scene_transition = false;

        private static TheGame _instance;

        void Awake()
        {
            _instance = this;
            PlayerData.LoadLast();
        }

        private void Start()
        {
            PlayerData pdata = PlayerData.Get();
            GameObject spawn_parent = new GameObject("SaveFileSpawns");

            //Spawn dropped items
            foreach (KeyValuePair<string, DroppedItemData> elem in pdata.dropped_items)
            {
                Item.Spawn(elem.Key, spawn_parent.transform);
            }

            //Spawn constructions
            foreach (KeyValuePair<string, BuiltConstructionData> elem in pdata.built_constructions)
            {
                Construction.Spawn(elem.Key, spawn_parent.transform);
            }

            //Spawn plants
            foreach (KeyValuePair<string, SowedPlantData> elem in pdata.sowed_plants)
            {
                Plant.Spawn(elem.Key, spawn_parent.transform);
            }

            //Spawn characters
            foreach (KeyValuePair<string, TrainedCharacterData> elem in pdata.trained_characters)
            {
                Character.Spawn(elem.Key, spawn_parent.transform);
            }

            //Set player and camera position
            if (!string.IsNullOrEmpty(pdata.current_scene) && pdata.current_scene == SceneNav.GetCurrentScene())
            {
                foreach (PlayerCharacter player in PlayerCharacter.GetAll())
                {
                    //Entry index: -1 = go to saved pos, 0=dont change character pos, 1+ = go to entry index
                    if (pdata.current_entry_index < 0)
                    {
                        player.transform.position = player.Data.position;
                        TheCamera.Get().MoveToTarget(player.Data.position);
                    }

                    if (pdata.current_entry_index > 0)
                    {
                        ExitZone zone = ExitZone.GetIndex(pdata.current_entry_index);
                        if (zone != null)
                        {
                            Vector3 pos = zone.transform.position + zone.entry_offset;
                            Vector3 dir = new Vector3(zone.entry_offset.x, 0f, zone.entry_offset.z);
                            player.transform.position = pos;
                            if (dir.magnitude > 0.1f)
                            {
                                player.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                                player.FaceTorward(transform.position + dir.normalized);
                            }
                            TheCamera.Get().MoveToTarget(pos);
                        }
                    }
                }
            }

            //Set current scene
            pdata.current_scene = SceneNav.GetCurrentScene();

            //Black panel transition
            if (!BlackPanel.Get().IsVisible())
            {
                BlackPanel.Get().Show(true);
                BlackPanel.Get().Hide();
            }

            //New game
            if (pdata.IsNewGame())
            {
                pdata.play_time = 0.01f; //Initialize play time to 0.01f to make sure onStartNewGame never get called again
                onStartNewGame?.Invoke(); //New Game!
            }
        }

        void Update()
        {
            if (TheGame.Get().IsPaused())
                return;

            //Check if dead
            PlayerCharacter character = PlayerCharacter.GetFirst();
            if (character && character.IsDead())
            {
                death_timer += Time.deltaTime;
                if (death_timer > 2f)
                {
                    enabled = false; //Stop running this loop
                    TheUI.Get().ShowGameOver();
                }
            }

            //Game time
            PlayerData pdata = PlayerData.Get();
            float game_speed = GetGameTimeSpeedPerSec();
            pdata.day_time += game_speed * Time.deltaTime;
            if (pdata.day_time >= 24f)
            {
                pdata.day_time = 0f;
                pdata.day++; //New day
            }

            //Play time
            pdata.play_time += Time.deltaTime;

            //Set music
            AudioClip[] music_playlist = GameData.Get().music_playlist;
            if (music_playlist.Length > 0 && !TheAudio.Get().IsMusicPlaying("music"))
            {
                AudioClip clip = music_playlist[Random.Range(0, music_playlist.Length)];
                TheAudio.Get().PlayMusic("music", clip, 0.4f, false);
            }

            //Inventory durability
            UpdateDurability();
        }

        private void UpdateDurability()
        {
            PlayerData pdata = PlayerData.Get();
            float game_speed = GetGameTimeSpeedPerSec();

            List<string> remove_items_uid = new List<string>();

            //Dropped
            foreach (KeyValuePair<string, DroppedItemData> pair in pdata.dropped_items)
            {
                DroppedItemData ddata = pair.Value;
                ItemData idata = ItemData.Get(ddata?.item_id);

                if (idata != null && ddata != null && idata.durability_type == DurabilityType.Spoilage)
                {
                    ddata.durability -= game_speed * Time.deltaTime;
                }

                if (idata != null && ddata != null && idata.HasDurability() && ddata.durability <= 0f)
                    remove_items_uid.Add(pair.Key);
            }

            foreach (string uid in remove_items_uid)
            {
                Item item = Item.GetByUID(uid);
                if (item != null)
                    item.SpoilItem();
            }
            remove_items_uid.Clear();

            //Inventory
            foreach (KeyValuePair<string, InventoryData> spair in pdata.inventories)
            {
                if (spair.Value != null)
                {
                    spair.Value.UpdateAllDurability(game_speed);
                }
            }

            //Constructions
            foreach (KeyValuePair<string, BuiltConstructionData> pair in pdata.built_constructions)
            {
                BuiltConstructionData bdata = pair.Value;
                ConstructionData cdata = ConstructionData.Get(bdata?.construction_id);

                if (cdata != null && bdata != null && (cdata.durability_type == DurabilityType.Spoilage || cdata.durability_type == DurabilityType.UsageTime))
                {
                    bdata.durability -= game_speed * Time.deltaTime;
                }

                if (cdata != null && bdata != null && cdata.HasDurability() && bdata.durability <= 0f)
                    remove_items_uid.Add(pair.Key);
            }

            foreach (string uid in remove_items_uid)
            {
                Construction item = Construction.GetByUID(uid);
                if (item != null)
                    item.Kill();
            }
            remove_items_uid.Clear();

            //Timed bonus
            foreach (KeyValuePair <int, PlayerCharacterData> pcdata in PlayerData.Get().player_characters)
            {
                List<BonusType> remove_bonus_list = new List<BonusType>();
                foreach (KeyValuePair<BonusType, TimedBonusData> pair in pcdata.Value.timed_bonus_effects)
                {
                    TimedBonusData bdata = pair.Value;
                    bdata.time -= game_speed * Time.deltaTime;

                    if (bdata.time <= 0f)
                        remove_bonus_list.Add(pair.Key);
                }
                foreach (BonusType bonus in remove_bonus_list)
                    pcdata.Value.RemoveTimedBonus(bonus);
                remove_bonus_list.Clear();
            }

            //World regrowth
            List<WorldRegrowthData> spawn_growth_list = new List<WorldRegrowthData>();
            foreach (KeyValuePair<string, WorldRegrowthData> pair in PlayerData.Get().world_regrowth)
            {
                WorldRegrowthData bdata = pair.Value;
                bdata.time -= game_speed * Time.deltaTime;

                if (bdata.time <= 0f && bdata.scene == SceneNav.GetCurrentScene())
                    spawn_growth_list.Add(pair.Value);
            }

            foreach (WorldRegrowthData regrowth in spawn_growth_list)
            {
                WorldRegrowth.SpawnRegrowth(regrowth);
                PlayerData.Get().RemoveWorldRegrowth(regrowth.uid);
            }
            spawn_growth_list.Clear();
        }

        public bool IsNight()
        {
            PlayerData pdata = PlayerData.Get();
            return pdata.day_time >= 18f || pdata.day_time < 6f;
        }

        //Set to 1f for default speed
        public void SetGameSpeedMultiplier(float mult)
        {
            speed_multiplier = mult;
        }

        //Game hours per real time hours
        public float GetGameTimeSpeed()
        {
            float game_speed = speed_multiplier * GameData.Get().game_time_mult;
            return game_speed;
        }

        //Game hours per real time seconds
        public float GetGameTimeSpeedPerSec()
        {
            float hour_to_sec = GetGameTimeSpeed() / 3600f;
            return hour_to_sec;
        }

        //---- Pause / Unpause -----

        public void Pause()
        {
            paused = true;
            paused_by_player = true;
            if (onPause != null)
                onPause.Invoke(paused);
        }

        public void Unpause()
        {
            paused = false;
            paused_by_player = false;
            if (onPause != null)
                onPause.Invoke(paused);
        }

        public void PauseScripts()
        {
            paused = true;
            paused_by_player = false;
        }

        public void UnpauseScripts()
        {
            paused = false;
            paused_by_player = false;
        }

        public bool IsPaused()
        {
            return paused;
        }

        public bool IsPausedByPlayer()
        {
            return paused_by_player;
        }

        public bool IsPausedByGameplay()
        {
            return paused && !paused_by_player;
        }

        //-- Scene transition -----

        public void TransitionToScene(string scene, int entry_index)
        {
            if (!scene_transition)
            {
                if (SceneNav.DoSceneExist(scene))
                {
                    scene_transition = true;
                    StartCoroutine(GoToSceneRoutine(scene, entry_index));
                }
                else
                {
                    Debug.Log("Scene don't exist: " + scene);
                }
            }
        }

        private IEnumerator GoToSceneRoutine(string scene, int entry_index)
        {
            BlackPanel.Get().Show();
            yield return new WaitForSeconds(1f);
            TheGame.GoToScene(scene, entry_index);
        }

        public static void GoToScene(string scene, int entry_index = 0)
        {
            if (!string.IsNullOrEmpty(scene)) {

                PlayerData pdata = PlayerData.Get();
                if (pdata != null)
                {
                    pdata.current_scene = scene;
                    pdata.current_entry_index = entry_index;
                }

                if (beforeChangeScene != null)
                    beforeChangeScene.Invoke(scene);

                SceneNav.GoTo(scene);
            }
        }

        //---- Load / Save -----

        //Save is not static, because a scene and save file must be loaded before you can save
        public void Save()
        {
            Save(PlayerData.Get().filename);
        }

        public bool Save(string filename)
        {
            if (!SaveSystem.IsValidFilename(filename))
                return false; //Failed

            foreach (PlayerCharacter player in PlayerCharacter.GetAll())
                player.Data.position = player.transform.position;

            PlayerData.Get().current_scene = SceneNav.GetCurrentScene();
            PlayerData.Get().current_entry_index = -1; //Go to saved current_pos instead of scene position

            if (beforeSave != null)
                beforeSave.Invoke(filename);

            PlayerData.Save(filename, PlayerData.Get());
            return true;
        }

        public static void Load()
        {
            Load(PlayerData.GetLastSave());
        }

        public static bool Load(string filename)
        {
            if (!SaveSystem.IsValidFilename(filename))
                return false; //Failed

            PlayerData.Unload(); //Make sure to unload first, or it won't load if already loaded
            PlayerData.AutoLoad(filename);

            if (afterLoad != null)
                afterLoad.Invoke();

            SceneNav.GoTo(PlayerData.Get().current_scene);
            return true;
        }

        public static void NewGame()
        {
            NewGame(PlayerData.GetLastSave(), SceneNav.GetCurrentScene());
        }

        public static bool NewGame(string filename, string scene)
        {
            if (!SaveSystem.IsValidFilename(filename))
                return false; //Failed

            PlayerData.NewGame(filename);

            if (afterNewGame != null)
                afterNewGame.Invoke();

            SceneNav.GoTo(scene);
            return true;
        }

        public static void DeleteGame(string filename)
        {
            PlayerData.Delete(filename);
        }

        //---------

        public static bool IsMobile()
        {
#if UNITY_ANDROID || UNITY_IOS || UNITY_TIZEN
            return true;
#elif UNITY_WEBGL
            return WebGLTool.isMobile();
#else
            return false;
#endif
        }

        public static TheGame Get()
        {
            if (_instance == null)
                _instance = FindObjectOfType<TheGame>();
            return _instance;
        }
    }

}