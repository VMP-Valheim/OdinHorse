using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace OdinHorse;

public class Patches
{
    internal static GameObject HorseObject;
    private ItemDrop HorseDrop;
    private static Container HorseContainer;
    private static Container NormalBox;

    private static Container tempBox;
    
    
    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
    public static class ObjectCopyPatch
    {
        public static void Prefix(ObjectDB __instance)
        {
            if (__instance.m_items.Count <= 0 || __instance.GetItemPrefab("Wood") == null) return;
            NormalBox = ZNetScene.instance.GetPrefab("piece_chest_wood").gameObject.GetComponent<Container>();
            if(NormalBox) AddContainer(HorseObject);
        }
    }

    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    public static class ObjectAwakePatch
    {
        public static void Prefix(ObjectDB __instance)
        {
            if (__instance.m_items.Count <= 0 || __instance.GetItemPrefab("Wood") == null) return;
            NormalBox = ZNetScene.instance.GetPrefab("piece_chest_wood").gameObject.GetComponent<Container>();
            if(NormalBox) AddContainer(HorseObject);
        }
    }

    private static void AddContainer(GameObject attachObject)
    {
        HorseContainer = attachObject.AddComponent<Container>();
        HorseContainer = new Container();
        HorseContainer.m_height = 4;
        HorseContainer.m_width = 6;
        HorseContainer.m_bkg = NormalBox.m_bkg;
        HorseContainer.m_inventory = new Inventory("HorseBox", HorseContainer.m_bkg, HorseContainer.m_height,
            HorseContainer.m_width);
        HorseContainer.m_rootObjectOverride = attachObject.GetComponent<ZNetView>();
        HorseContainer.m_name = "SaddleBags";
    }

    

    [HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact))]
    public static class InteractPatch
    {
        public static bool Prefix(Tameable __instance)
        {
            if (__instance.gameObject.name.StartsWith("rae_OdinHorse"))
            {
                if (!__instance.gameObject.GetComponent<Humanoid>().IsTamed()) return true;
                var container = __instance.gameObject.GetComponent<Container>();
                if (Input.GetKey(KeyCode.P))
                {
                    container.Interact(Player.m_localPlayer, false, false);
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
    public static class HoverTextPatch
    {
        public static void Postfix(ref string __result, Tameable __instance)
        {
            if (!__instance.gameObject.name.StartsWith("rae_OdinHorse")) return;
            if (!__instance.gameObject.GetComponent<Humanoid>().IsTamed()) return;
            __result += global::Localization.instance.Localize("\n<color=yellow>P & $KEY_Use  to open container</color>");
        }
    }

    [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.OnDeath))]
    public static class DropHorseStuffPatch
    {
        public static void Prefix(CharacterDrop __instance)
        {
            if (!__instance.gameObject.name.StartsWith("rae_OdinHorse")) return;
            if(Player.m_localPlayer == null) return;
            var znvew = __instance.gameObject.GetComponent<ZNetView>();
            if (!znvew.IsOwner()) return;
            DropItemsOnDeath(__instance.gameObject.GetComponent<Container>());
        }
    }
    private static void DropItemsOnDeath(Container box)
    {
        var position = new Vector3(Player.m_localPlayer.transform.position.x +2, Player.m_localPlayer.transform.position.y, Player.m_localPlayer.transform.position.z);
        var newbox = OdinHorse.Instantiate(Patches.NormalBox.gameObject, position,
            Quaternion.identity, null);
        var boxstone = ZNetScene.instance.GetPrefab("Player_tombstone");
        var GraveStoneMesh = boxstone.transform.Find("Group8801").gameObject;
        var GOCanvas = boxstone.transform.Find("Canvas").gameObject;
        
        var container = newbox.GetComponent<Container>();
        container.m_open = null;
        container.m_closed = null;
        
        var containerMesh = newbox.transform.Find("New/woodchest").gameObject;
        var NewTransfomr = newbox.transform.Find("New");
        newbox.transform.Find("New/woodchesttop_closed").gameObject.SetActive(false);
        Object.DestroyImmediate(containerMesh);
        var tempObj =Object.Instantiate(GraveStoneMesh, NewTransfomr, false);
        var tempCanvas= Object.Instantiate(GOCanvas, newbox.transform, false);
        tempCanvas.transform.Find("Text").gameObject.GetComponent<Text>().text = "SaddleBags";
        foreach (var Idata in box.m_inventory.GetAllItems())
        {
            container.m_inventory.m_inventory.Add(Idata);
        }

        container.m_autoDestroyEmpty = true;
        container.m_name = "SaddleBag";
    }
    
}