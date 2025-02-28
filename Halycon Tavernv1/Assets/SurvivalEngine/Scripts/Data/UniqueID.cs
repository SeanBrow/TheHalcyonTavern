﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{

    /// <summary>
    /// Helps to generate unique ids for each individual instance of objects in the scene. 
    /// Unique IDs are mostly used in the save file to keep track of the state of an object.
    /// </summary>

    public class UniqueID : MonoBehaviour
    {
        public string uid_prefix; //Will be added to the front of every ID of this type of object, set in the prefab

        [TextArea(1, 2)]
        public string unique_id; //The unique ID, should be empty in the prefab. Should only be added to instances in the scene. Can be automatically generated

        private static Dictionary<string, UniqueID> dict_id = new Dictionary<string, UniqueID>();

        void Awake()
        {
            if (unique_id != "")
            {
                dict_id[unique_id] = this;
            }
        }

        private void OnDestroy()
        {
            dict_id.Remove(unique_id);
        }

        private void Start()
        {
            if (HasUID() && PlayerData.Get().IsObjectHidden(unique_id))
                gameObject.SetActive(false);

            if (!HasUID() && Time.time < 0.1f)
                Debug.LogWarning("UID is empty on " + gameObject.name + ". Make sure to generate UIDs with SurvivalEngine->Generate UID");
        }

        public void Hide()
        {
            PlayerData.Get().HideObject(unique_id);
            gameObject.SetActive(false);
        }

        public void Show()
        {
            PlayerData.Get().ShowObject(unique_id);
            gameObject.SetActive(true);
        }

        public void SetValue(int value)
        {
            PlayerData.Get().SetCustomValue(unique_id, value);
        }

        public int GetValue()
        {
            return PlayerData.Get().GetCustomValue(unique_id);
        }

        public bool HasValue()
        {
            return PlayerData.Get().HasCustomValue(unique_id);
        }

        public bool HasUID()
        {
            return !string.IsNullOrEmpty(unique_id);
        }

        public void GenerateUID()
        {
            unique_id = uid_prefix + GenerateUniqueID();
        }

        public static string GenerateUniqueID(int min=11, int max=17)
        {
            int length = Random.Range(min, max);
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            string unique_id = "";
            for (int i = 0; i < length; i++)
            {
                unique_id += chars[Random.Range(0, chars.Length - 1)];
            }
            return unique_id;
        }

        public static void GenerateAll(UniqueID[] objs)
        {
            HashSet<string> existing_ids = new HashSet<string>();

            foreach (UniqueID uid_obj in objs)
            {
                if (uid_obj.unique_id != "")
                {
                    if (existing_ids.Contains(uid_obj.unique_id))
                        uid_obj.unique_id = "";
                    else
                        existing_ids.Add(uid_obj.unique_id);
                }
            }

            foreach (UniqueID uid_obj in objs)
            {
                if (uid_obj.unique_id == "")
                {
                    //Generate new ID
                    string new_id = "";
                    while (new_id == "" || existing_ids.Contains(new_id))
                    {
                        new_id = UniqueID.GenerateUniqueID();
                    }

                    //Add new id
                    uid_obj.unique_id = uid_obj.uid_prefix + new_id;
                    existing_ids.Add(new_id);

#if UNITY_EDITOR
                    if (Application.isEditor && !Application.isPlaying)
                        UnityEditor.EditorUtility.SetDirty(uid_obj);
#endif
                }
            }
        }

        public static void ClearAll(UniqueID[] objs)
        {
            foreach (UniqueID uid_obj in objs)
            {
                uid_obj.unique_id = "";

#if UNITY_EDITOR
                if (Application.isEditor && !Application.isPlaying)
                    UnityEditor.EditorUtility.SetDirty(uid_obj);
#endif
            }
        }

        public static bool HasID(string id)
        {
            return dict_id.ContainsKey(id);
        }

        public static GameObject GetByID(string id)
        {
            if (dict_id.ContainsKey(id))
            {
                return dict_id[id].gameObject;
            }
            return null;
        }
    }

}