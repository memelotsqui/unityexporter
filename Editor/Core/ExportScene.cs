using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

//using Unity.EditorCoroutines.Editor;
using UnityEngine;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Linq;
using System;
using Newtonsoft.Json;

public class ExportScene : EditorWindow
{
    public static ExportScene instance;

    public enum State
    {
        INITIAL,
        PRE_EXPORT,
        EXPORTING,
        POST_EXPORT,
        RESTORING,
        ERROR
    }

    State state;
    bool doDebug;
    string defaultMatPath = @"Assets/Webaverse/Materials/Block.mat";
    string nullMatPath = @"Assets/Webaverse/Materials/Null.mat";

    [MenuItem("Webaverse/Export Scene")]
    static void Init()
    {
        ExportScene window = (ExportScene)EditorWindow.GetWindow(typeof(ExportScene));
        window.state = State.INITIAL;
        instance = window;
        EditorSceneManager.activeSceneChanged += (x1, x2) => OnChangeMainScene();
        EditorSceneManager.sceneOpened += (scene, mode) =>
        {
            OnChangeMainScene();
        };
        window.Show();
    }

    private static void OnChangeMainScene()
    {
        UnityEngine.Debug.Log("on change scene");
        var data = new PipelineSettings.Data();
        data.Set();
        if(!PipelineSettings.ReadSettingsFromConfig())
        {
            data.Apply();
        }
    }

    Material _targetMat;

    Exporter exporter;

    public string ConversionPath => Path.Combine(PipelineSettings.ConversionFolder, PipelineSettings.GLTFName);
    
    //public string ExportPath
    //{
    //    get
    //    {
    //        string exportFolder = PipelineSettings.ProjectFolder + "/assets/";

    //        return exportFolder;
    //    }
    //}
    
    private void OnFocus()
    {
        if(exporter == null)
        {
            exporter = new Exporter();
        }

        Config.Load();


        ExtensionManager.Init();
    }

    Texture2D targTex, result;
    bool showAdvancedOptions, showOptimization, baseSettings;
    bool savePersistentSelected;
    private void OnGUI()
    {
        #region Initial Menu
        switch (state)
        {

            case State.INITIAL:
                PipelineSettings.ExportLights = EditorGUILayout.Toggle("Lights", PipelineSettings.ExportLights);
                GUI.enabled = false;
                EditorGUILayout.LabelField("*Coming soon :)", WEBAGuiStyles.CustomColorLabel(false,true,true,Color.yellow));
                PipelineSettings.ExportColliders = EditorGUILayout.Toggle("Colliders", PipelineSettings.ExportColliders);
                PipelineSettings.ExportSkybox = EditorGUILayout.Toggle("Skybox", PipelineSettings.ExportSkybox);
                PipelineSettings.ExportEnvmap = EditorGUILayout.Toggle("Envmap", PipelineSettings.ExportEnvmap);
                GUI.enabled = true;
                GUILayout.Space(8);
                PipelineSettings.meshMode = (MeshExportMode)EditorGUILayout.EnumPopup("Mesh Export Options", PipelineSettings.meshMode);
                PipelineSettings.lightmapMode = (LightmapMode)EditorGUILayout.EnumPopup("Lightmap Mode", PipelineSettings.lightmapMode);
                // Add a label to indication that the BAKE_SEPARATE lightmap mode will export MOZ_lightmap extension
                if (PipelineSettings.lightmapMode != LightmapMode.BAKE_SEPARATE)
                {
                    EditorGUILayout.HelpBox("The MOZ_lightmap extension is only supported with BAKE_SEPARATE", MessageType.Info);
                }
                GUILayout.Space(8);
                PipelineSettings.CombinedTextureResolution = EditorGUILayout.IntField("Max Texture Resolution", PipelineSettings.CombinedTextureResolution);
                
                
                GUILayout.Space(16);
                showOptimization = false;
                //showOptimization = EditorGUILayout.Foldout(showOptimization, "GLTF Optimization");
                //
                if (showOptimization)
                {
                    PipelineSettings.InstanceMeshes = EditorGUILayout.Toggle("Instanced Meshes", PipelineSettings.InstanceMeshes);

                    PipelineSettings.MeshOptCompression = EditorGUILayout.Toggle("MeshOpt Compression", PipelineSettings.MeshOptCompression);

                    PipelineSettings.MeshQuantization = EditorGUILayout.Toggle("Mesh Quantization", PipelineSettings.MeshQuantization);

                    PipelineSettings.KTX2Compression = EditorGUILayout.Toggle("KTX2 Compression", PipelineSettings.KTX2Compression);

                    PipelineSettings.CombineMaterials = EditorGUILayout.Toggle("Combine Materials (breaks lightmaps)", PipelineSettings.CombineMaterials);

                    PipelineSettings.CombineNodes = EditorGUILayout.Toggle("Combine Nodes (breaks Webaverse components)", PipelineSettings.CombineNodes);
                    GUILayout.Space(16);
                    PipelineSettings.ExportFormat = (ExportFormat)EditorGUILayout.EnumPopup("Exported File Format", PipelineSettings.ExportFormat);
                    GUILayout.Space(8);
                    PipelineSettings.BasicGLTFConvert = EditorGUILayout.Toggle("Use Basic GLTF Converter", PipelineSettings.BasicGLTFConvert);
                    GUILayout.Space(16);
                }

                showAdvancedOptions = false;
                //showAdvancedOptions = EditorGUILayout.Foldout(showAdvancedOptions, "Advanced Tools");
                if (showAdvancedOptions)
                {

                    savePersistentSelected = GUILayout.Toggle(savePersistentSelected, "Serialize into Persistent Assets (Are not deleted after export)");
                    
                    if (GUILayout.Button("Serialize Selected Assets"))
                    {
                        SerializeSelectedAssets(savePersistentSelected);
                    }
                    GUILayout.Space(8);
                    if(GUILayout.Button("Serialize All Assets"))
                    {
                        SerializeAllMaterials(savePersistentSelected);
                        CreateUVBakedMeshes(savePersistentSelected);
                    }
                    GUILayout.Space(8);
                    if(GUILayout.Button("Deserialize Selected Assets"))
                    {
                        DeserializeSelectedAssets();
                    }
                    GUILayout.Space(8);
                    if (GUILayout.Button("Deserialize All Assets"))
                    {
                        RestoreAllGLLinks();
                        DeserializeAllMaterials();
                    }
                    GUILayout.Space(8);
                    if (PipelineSettings.meshMode == MeshExportMode.COMBINE)
                    {
                        PipelineSettings.preserveLightmapping = EditorGUILayout.ToggleLeft("Preserve Lightmapping", PipelineSettings.preserveLightmapping);
                    }
                    GUILayout.BeginVertical();
                    if (GUILayout.Button("Do MeshBake"))
                    {
                        SerializeAllMaterials(true);
                        CreateUVBakedMeshes(true);
                        CombineMeshes(true);
                    }
                    GUILayout.Space(8);
                    if (GUILayout.Button("Format LODGroups"))
                    {
                        LODFormatter.FormatLODs();
                    }
                    GUILayout.Space(8);
                    if(GUILayout.Button("Convert LODs to Instancing"))
                    {
                        LODFormatter.ConvertToInstancing();
                    }
                    GUILayout.Space(8);
                    
                    if (GUILayout.Button("Undo MeshBake"))
                    {
                        CleanupMeshCombine();
                        RestoreAllGLLinks();
                        DeserializeAllMaterials();
                    }
                    GUILayout.Space(16);
                    if (GUILayout.Button("Revert Backups"))
                    {
                        var mats = FindObjectsOfType<GameObject>().SelectMany((go) => go.GetComponent<MeshRenderer>() ? go.GetComponent<MeshRenderer>().sharedMaterials : new Material[0])
                            .Distinct().ToArray();
                        foreach (var mat in mats)
                        {
                            string assetPath = AssetDatabase.GetAssetPath(mat);
                            if (!string.IsNullOrEmpty(assetPath))
                            {
                                string prefix = Regex.Replace(assetPath, @"(?<=.*\/+)([\w\d\._ -]*).mat$", "$1_$1_bak.mat");
                                //prefix = assetPath.Replace(".mat", "_bak.mat");
                                prefix = Regex.Replace(prefix, @"(?<=.*)\/+(?=[\w\d_\. -]*$)", "/bak/");
                            }
                        }
                    }

                    doDebug = EditorGUILayout.Toggle("Debug Execution", doDebug);

                    GUILayout.EndVertical();
                }
                GUILayout.Space(8);
                // Create a Name TextField with a default value of "scene"
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Name: ", WEBAGuiStyles.CustomLabel(false, true, false), GUILayout.Width(80f));
                PipelineSettings.GLTFName = EditorGUILayout.TextField(String.IsNullOrEmpty(PipelineSettings.GLTFName) ? "scene" : PipelineSettings.GLTFName, GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(5);

                if (PipelineSettings.ProjectFolder != "")
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Export To: ", WEBAGuiStyles.CustomLabel(false, true, false), GUILayout.Width(80f));
                    EditorGUILayout.LabelField(PipelineSettings.ProjectFolder,WEBAGuiStyles.CustomLabel(false,false,true));
                    EditorGUILayout.EndHorizontal();
                }
                GUILayout.Space(8);
                if (GUILayout.Button("Set Output Directory", GUILayout.Height(30f)))
                {
                    string dir = EditorUtility.SaveFolderPanel("Output Directory", PipelineSettings.ProjectFolder, "");
                    if (dir != "")
                        PipelineSettings.ProjectFolder = dir;
                }
                GUILayout.Space(8);


                if (PipelineSettings.ProjectFolder != null)
                {
                    if (GUILayout.Button("Export Scene", GUILayout.Height(30f)))
                    {
                        state = State.PRE_EXPORT;
                        Export(false, true);
                    }
                    if (Selection.activeGameObject == null)
                        GUI.enabled = false;
                    if (GUILayout.Button("Export Selected", GUILayout.Height(30f)))
                    {
                        state = State.PRE_EXPORT;
                        Export(false,false);
                    }
                    GUI.enabled = true;
                } else {
                    EditorGUILayout.HelpBox("Please select an output directory", MessageType.Warning);
                }

                GUILayout.Space(8);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Refresh Settings", GUILayout.Height(30f), GUILayout.MinWidth(100f)))
                {
                    PipelineSettings.ReadSettingsFromConfig();
                }
                if (GUILayout.Button("Save Settings", GUILayout.Height(30f), GUILayout.MinWidth(100f)))
                {
                    PipelineSettings.SaveSettings();
                }
                EditorGUILayout.EndHorizontal();
                break;


            #endregion

            #region Debugging Stepper
            case State.PRE_EXPORT:
                if(GUILayout.Button("Continue"))
                {
                    state++;
                }
                break;

            case State.POST_EXPORT:
                if (GUILayout.Button("Continue"))
                {
                    state++;
                }
                break;
            #endregion
            default:
                GUILayout.Label("Exporting...");
                break;
        }
    }

    #region UTILITY FUNCTIONS

    Dictionary<Material, Material> matLinks;
    Dictionary<string, Material> matRegistry;
    private Material BackupMaterial(Material material, Renderer _renderer, bool savePersistent)
    {
        if (matRegistry == null) matRegistry = new Dictionary<string, Material>();
        bool hasLightmap = WebaUnity.HasLightmap(_renderer);
        string registryKey = string.Format("{0}_{1}", material.name, hasLightmap ? _renderer.lightmapIndex : -2);
        
        if (matRegistry.ContainsKey(registryKey))
        {
            return matRegistry[registryKey];
        }
            
        
        string origPath = AssetDatabase.GetAssetPath(material);
        if (origPath == null || Regex.IsMatch(origPath, @".*\.[(glb)(fbx)]"))
        {

            UnityEngine.Debug.Log("Creating link Material to glb");
            Material dupe = new Material(material);
            string dupeRoot = savePersistent ? PipelineSettings.PipelinePersistentFolder : PipelineSettings.PipelineAssetsFolder;
            string dupePath = dupeRoot + material.name + "_" + DateTime.Now.Ticks + ".mat";
            string dupeDir = Regex.Match(dupePath, @"(.*[\\\/])[\w\.\d\-]+").Value;
            dupePath = dupePath.Replace(Application.dataPath, "Assets");
            if(!Directory.Exists(dupeDir))
            {
                Directory.CreateDirectory(dupeDir);
            }
            AssetDatabase.CreateAsset(dupe, dupePath);

            if(matLinks == null)
            {
                matLinks = new Dictionary<Material, Material>();
            }

            matLinks[dupe] = material;
            UnityEngine.Debug.Log("material " + dupe.name + " linked to material " + material.name);
            matRegistry[registryKey] = dupe;
            Material check = material;
            foreach (var renderer in FindObjectsOfType<Renderer>())
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select((sharedMat) =>
                    sharedMat &&
                    sharedMat.name == check.name ? dupe : sharedMat
                ).ToArray();
            }

            return dupe;
        }
        return material;
    }

    

    private Tuple<Material, string, string>[] BackupTextures(ref Material mat, bool savePersistent)
    {
        //Material mat = _mat;
        /*
        string[] mapTests = new string[]
        {
            "_MainTex",
            "_BumpMap",
            "_EmissionMap",
            "_MetallicGlossMap",
            "_OcclusionMap",
            "_baseColorMap"
        };
        */
        var maps = mat.GetTexturePropertyNames();
        var matTexes = new List<Texture>();
        for(int i = 0; i < maps.Length; i++)
        {
            matTexes.Add(mat.GetTexture(maps[i]));
        }
        var textures = maps
            .Select((map, i) =>
            {
                Texture tex = matTexes[i];
                if (tex == null) return null;
                return new Tuple<string, Texture>(map, tex);
            })
            .Where((x) => x != null && x.Item2.GetType() == typeof(Texture2D))
            .Select((x) => new Tuple<string, Texture2D>(x.Item1, (Texture2D)x.Item2))
            .ToArray();
        var texPaths = new List<Tuple<Material, string, string>>();
        foreach(var texture in textures)
        {
            var tex = texture.Item2;
            string texPath = AssetDatabase.GetAssetPath(tex);
            if (texPath == null || texPath == "" || Regex.IsMatch(texPath, @".*\.[(glb)(fbx)]"))
            {
                string nuPath;
                Texture2D nuTex = GenerateAsset(tex, out nuPath, savePersistent);
                texPaths.Add(new Tuple<Material, string, string>(mat, texture.Item1, nuPath));
            }
        }
        return texPaths.ToArray();
    }

    Dictionary<Texture2D, Texture2D> texLinks;
    Dictionary<Texture2D, Tuple<Texture2D, string>> texRegistry;
    public Texture2D GenerateAsset(Texture2D tex, out string path, bool savePersistent)
    {
        if(texRegistry != null && texRegistry.ContainsKey(tex))
        {
            var registry = texRegistry[tex];
            path = registry.Item2;
            return registry.Item1;
        }
        Texture2D nuTex = new Texture2D(tex.width, tex.height, tex.format, tex.mipmapCount, false);
        nuTex.name = tex.name + "_" + System.DateTime.Now.Ticks;
        Graphics.CopyTexture(tex, nuTex);
        nuTex.Apply();
        string pRoot = savePersistent ? PipelineSettings.PipelinePersistentFolder : PipelineSettings.PipelineAssetsFolder;
        if (!Directory.Exists(pRoot))
        {
            Directory.CreateDirectory(pRoot);
        }
        string nuPath = pRoot.Replace(Application.dataPath, "Assets") + nuTex.name + ".png";
        File.WriteAllBytes(nuPath, nuTex.EncodeToPNG());

        UnityEngine.Debug.Log("Generated texture " + nuTex + " from " + tex);
        if (texLinks == null)
            texLinks = new Dictionary<Texture2D, Texture2D>();
        if (texRegistry == null)
            texRegistry = new Dictionary<Texture2D, Tuple<Texture2D, string>>();
        texLinks[nuTex] = tex;
        texRegistry[tex] = new Tuple<Texture2D, string>(tex, nuPath);
        path = nuPath;
        return nuTex;
    }
    #endregion

    #region LODS
    Dictionary<Transform, string> lodRegistry;
    private void FormatForExportingLODs()
    {
        lodRegistry = new Dictionary<Transform, string>();
        LODGroup[] lodGroups = GameObject.FindObjectsOfType<LODGroup>().Where((lg) => lg.gameObject.activeInHierarchy && lg.enabled).ToArray();
        foreach(var lodGroup in lodGroups)
        {
            Transform tr = lodGroup.transform;
            lodRegistry.Add(tr, tr.name);
            if(!Regex.IsMatch(tr.name, @".*_LODGroup"))
                tr.name += "_LODGroup";
            var children = Enumerable.Range(0, tr.childCount).Select((i) => tr.GetChild(i)).ToArray();
            foreach(var child in children)
            {
                child.name = Regex.Match(child.name, @"^.*LOD\d+").Value;
            }
        }
    }

    private void CleanupExportingLODs()
    {
        if(lodRegistry != null)
        {
            foreach(var kv in lodRegistry)
            {
                kv.Key.name = kv.Value;
            }
            lodRegistry = null;
        }
    }
    #endregion

    #region LIGHTS
    Light[] bakeLights;
    public void StageLights()
    {
        bakeLights = FindObjectsOfType<Light>().Where((light) => light.gameObject.activeInHierarchy && light.lightmapBakeType == LightmapBakeType.Baked).ToArray();

        foreach (var light in bakeLights)
        {
            light.gameObject.SetActive(false);
        }
    }

    Light[] realtimeLights;
    public void StageRealtimeLights()
    {
        UnityEngine.Debug.Log("stages realtime lights");
        realtimeLights = FindObjectsOfType<Light>().Where((light) => light.gameObject.activeInHierarchy && light.lightmapBakeType == LightmapBakeType.Realtime).ToArray();
        

        foreach (var light in realtimeLights)
        {
            light.gameObject.SetActive(false);
        }
    }

    public void CleanupRealtimeLights()
    {
        foreach (var light in realtimeLights)
        {
            light.gameObject.SetActive(true);
        }
        realtimeLights = null;
    }

    public void CleanupLights()
    {
        foreach(var light in bakeLights)
        {
            light.gameObject.SetActive(true);
        }
        bakeLights = null;
    }
    #endregion

    #region LIGHTMAPPING

    List<IgnoreLightmap> ignoreChildren;
    private void StageLightmapping()
    {
        MOZ_lightmap_Factory.ClearRegistry();

        if(ignoreChildren == null)
        {
            ignoreChildren = new List<IgnoreLightmap>();
        }
        var ignorers = FindObjectsOfType<IgnoreLightmap>().Where((ignorer) => ignorer.applyToChildren);

        foreach(var ignorer in ignorers)
        {
            ignoreChildren.AddRange(WebaUnity.ChildComponents<MeshRenderer>(ignorer.transform)
                .Select(renderer => renderer.gameObject.AddComponent<IgnoreLightmap>()));
        }
    }

    private void CleanupLightmapping()
    {
        if(ignoreChildren != null)
        {
            foreach(var child in ignoreChildren)
            {
                DestroyImmediate(child);
            }
            ignoreChildren = null;
        }

        MOZ_lightmap_Factory.ClearRegistry();
    }
    #endregion

    #region SKYBOX
    private void FormatForExportingSkybox()
    {
        if (PipelineSettings.ExportSkybox)
        {
            // INFO TO SAVE DATA
            string[] fNames = new string[]
                {
                "negx",
                "posx",
                "posy",
                "negy",
                "posz",
                "negz"
                };
            string nuPath = Path.Combine(PipelineSettings.ProjectFolder, "cubemap");
            if(!Directory.Exists(nuPath))
            {
                Directory.CreateDirectory(nuPath);
            }
            SkyBox.Mode outMode = SkyBox.Mode.CUBEMAP;

            // THE CUBEMAP
            var skyMat = RenderSettings.skybox;
            if (skyMat.shader.name == "Skybox/6 Sided")
            {
                string[] texNames = new[]
                {
                    "_FrontTex",
                    "_BackTex",
                    "_UpTex",
                    "_DownTex",
                    "_LeftTex",
                    "_RightTex",
                };
                string[] faceTexes = texNames.Select((x, i) =>
                {
                    string facePath = string.Format("{0}/{1}.jpg", nuPath, fNames[i]);
                    UnityEngine.Debug.Log(facePath);
                    if (skyMat.GetTexture(x) != null)
                        File.WriteAllBytes(facePath, ((Texture2D)skyMat.GetTexture(x)).EncodeToJPG());
                    else
                        File.WriteAllBytes(facePath, TextureConverter.CreateFromColor(skyMat.GetColor("_Tint"), 128, 128).EncodeToJPG());
                    return x;
                }).ToArray();
            }
            else if(skyMat.shader.name.Contains("Skybox/Panoramic"))
            {
                var hdri = skyMat.GetTexture("_MainTex") as Texture2D;
                outMode = SkyBox.Mode.EQUIRECTANGULAR;
                string outPath = nuPath + "/rect.jpg";
                File.WriteAllBytes(outPath, hdri.EncodeToJPG(100));
            }
            else
            {
                if(!skyMat.HasTexture("_Tex")){
                    UnityEngine.Debug.Log("Invalid skymap material, missing _Tex attribute");
                } else {
                var cubemap = skyMat.GetTexture("_Tex") as Cubemap;
                string srcPath = AssetDatabase.GetAssetPath(cubemap);
                string srcName = Regex.Match(srcPath, @"(?<=.*/)\w*(?=\.hdr)").Value;
                
                var cubemapDir = new DirectoryInfo(nuPath);
                if (!cubemapDir.Exists)
                {
                    cubemapDir.Create();
                }

                CubemapFace[] faces = Enumerable.Range(0, 6).Select((i) => (CubemapFace)i).ToArray();
                
                Texture2D[] faceTexes = faces.Select((x, i) =>
                {
                    Texture2D result = new Texture2D(cubemap.width, cubemap.height);// cubemap.format, false);
                    var pix = cubemap.GetPixels(x);
                    System.Array.Reverse(pix);
                    result.SetPixels(pix);
                    result.Apply();

                    string facePath = string.Format("{0}/{1}.jpg", nuPath, fNames[i]);
                    File.WriteAllBytes(facePath, result.EncodeToJPG());
                    return result;
                }).ToArray();
                }
            }
            

            GameObject skyboxGO = new GameObject("__skybox__");
            skyboxGO.AddComponent<SkyBox>().mode = outMode;
        }
    }
        
    private void CleanupExportingSkybox()
    {
        var skyboxes = FindObjectsOfType<SkyBox>();
        for(int i = 0; i < skyboxes.Length; i++)
        {
            DestroyImmediate(skyboxes[i].gameObject);
        }
    }
    #endregion

    #region ENVMAP
    private void FormatForExportingEnvmap()
    {
        GameObject envmapGO = new GameObject("__envmap__");
        envmapGO.AddComponent<Envmap>();
    }

    private void CleanupExportEnvmap()
    {
        var envmaps = FindObjectsOfType<Envmap>();
        for (int i = 0; i < envmaps.Length; i++)
        {
            DestroyImmediate(envmaps[i].gameObject);
        }
    }
    #endregion

    #region MESH BAKING
    class MatRend
    {
        public Material mat;
        public Renderer rend;
        public MatRend(Material _mat, Renderer _rend)
        {
            mat = _mat;
            rend = _rend;
        }
    }

    private void SerializeSelectedAssets(bool savePersistent = false)
    {
        UnityEngine.Debug.LogWarning("serialize selected");
        var renderers = Selection.gameObjects.Select((go) => go.GetComponent<Renderer>()).Where((rend) => rend != null);
        foreach(var renderer in renderers)
        {
            var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh != null)
            {
                GenerateMesh(renderer, mesh, savePersistent);
            }
        }
        SerializeMaterials(renderers, savePersistent);
    }

    private void DeserializeSelectedAssets()
    {
        var renderers = Renderers;
        DeserializeMaterials(renderers);
        RestoreGLLinks(renderers);
    }

    private void SerializeMaterials(IEnumerable<Renderer> renderers, bool savePersistent = false)
    {
        matRegistry = matRegistry != null ? matRegistry : new Dictionary<string, Material>();
        matLinks = matLinks != null ? matLinks : new Dictionary<Material, Material>();
        texLinks = texLinks != null ? texLinks : new Dictionary<Texture2D, Texture2D>();
        var mats = renderers
            .SelectMany((rend) => rend.sharedMaterials
            .Select((mat) => new MatRend(mat, rend)))
            .Where((x) => x != null && x.mat != null && x.rend != null && x.rend.enabled && x.rend.gameObject.activeInHierarchy)
            .ToArray();

        for (int i = 0; i < mats.Length; i++)
        {
            var mat = mats[i].mat;
            var rend = mats[i].rend;
            mats[i].mat = BackupMaterial(mat, rend, savePersistent);
        }
        AssetDatabase.Refresh();
        var remaps = new List<Tuple<Material, string, string>>();
        for (int i = 0; i < mats.Length; i++)
        {
            var mat = mats[i].mat;
            remaps.AddRange(BackupTextures(ref mat, savePersistent));
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        for (int i = 0; i < remaps.Count; i++)
        {
            var remap = remaps[i];
            var nuMat = remap.Item1;
            var nuTex = AssetDatabase.LoadAssetAtPath<Texture2D>(remap.Item3);
            GLTFUtilities.SetTextureImporterFormat(nuTex, true);
            nuMat.SetTexture(remap.Item2, nuTex);
            UnityEngine.Debug.Log("material " + nuMat + " path of " + AssetDatabase.GetAssetPath(nuMat));
            UnityEngine.Debug.Log("setting material " + nuMat + " texture " + remap.Item2 + " to " + nuTex + ", path of " + AssetDatabase.GetAssetPath(nuTex));

        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        UnityEngine.Debug.Log("processed materials");
        var rgroups = mats.GroupBy((mat) => mat.rend).Select((rGroup) =>
        new Dictionary<Renderer, Material[]>
        (
            new KeyValuePair<Renderer, Material[]>[]
            {
                new KeyValuePair<Renderer, Material[]>
                (
                    rGroup.Key,
                    rGroup.Select((r) => r.mat).ToArray()
                )
            }
        ));
        if (rgroups.Count() > 0)
        {
            UnityEngine.Debug.Log(rgroups);
            var updates = rgroups.Aggregate((rGroup1, rGroup2) =>
            {
                if (rGroup2 == null) return rGroup1;
                if (rGroup1 == null) return rGroup2;
                foreach (var key in rGroup2.Keys)
                {
                    rGroup1[key] = rGroup2[key];
                }
                return rGroup2;
            });
            if (updates != null)
                foreach (var update in updates)
                {
                    update.Key.sharedMaterials = update.Value;
                }
        }
    }
    private List<Renderer> Renderers { get
        {
            List<Renderer> renderers = FindObjectsOfType<Renderer>().ToList();
            renderers = renderers.Where((x) => x.gameObject.activeInHierarchy && x.enabled).ToList();
            return renderers;
        } }
    private void SerializeAllMaterials(bool savePersistent = false)
    {
        UnityEngine.Debug.Log(Renderers.Count());
        SerializeMaterials(Renderers, savePersistent);
    }

    private void DeserializeMaterials(IEnumerable<Renderer> renderers)
    {
        HashSet<Material> toRemove = new HashSet<Material>();
        foreach (var renderer in renderers)
        {
            renderer.sharedMaterials = renderer.sharedMaterials.Select
            (
                (mat) =>
                {
                    if (matLinks.ContainsKey(mat))
                    {
                        toRemove.Add(mat);
                        mat = matLinks[mat];
                    }
                    return mat;
                }
            ).ToArray();
        }
        foreach (var material in toRemove)
        {
            matLinks.Remove(material);
        }
    }

    private void DeserializeAllMaterials()
    {
        DeserializeMaterials(Renderers);
        matRegistry = null;
        matLinks = null;
        texRegistry = null;
        texLinks = null;
    }

    private void CreateBakedMeshes(bool savePersistent)
    {
        if(PipelineSettings.meshMode == MeshExportMode.DEFAULT ||
            PipelineSettings.meshMode == MeshExportMode.COMBINE)
        {
            CreateUVBakedMeshes(savePersistent);
            if(PipelineSettings.meshMode == MeshExportMode.COMBINE)
            {
                CombineMeshes();
            }
        }
    }

    struct MeshRegistryKey
    {
        public Mesh mesh;
        public string registryID;

        public override bool Equals(object _other)
        {
            if (_other == null) return false;
            var other = (MeshRegistryKey)_other;
            return mesh == other.mesh && registryID == other.registryID;
        }

        public override int GetHashCode()
        {
            return mesh.GetHashCode() ^ registryID.GetHashCode();
        }

        public MeshRegistryKey(Mesh mesh, Renderer renderer)
        {
            this.mesh = mesh;
            bool hasLightmap = WebaUnity.HasLightmap(renderer);
            bool hasTxrOffset = renderer.sharedMaterial != null &&
                (renderer.sharedMaterial.mainTextureOffset != Vector2.one ||
                    renderer.sharedMaterial.mainTextureScale != Vector2.one);
            int lightIdx = hasLightmap ? renderer.lightmapIndex : -2;
            // Random value required: Every mesh with lightmap must have its own mesh instance, cant share the same source mesh.
            int randVal = hasLightmap ? UnityEngine.Random.Range(0,999999) : 0;
            Vector2 txrOffset = hasTxrOffset ? renderer.sharedMaterial.mainTextureOffset : Vector2.negativeInfinity;
            registryID = String.Format("%d_%f_%f", lightIdx, txrOffset.x, txrOffset.y) + randVal.ToString();
        }
    }

    private Mesh GenerateMesh(Renderer renderer, Mesh mesh, bool savePersistent = false)
    {
        glLinks = glLinks != null ? glLinks : new Dictionary<Mesh, Mesh>();
        glRegistry = glRegistry != null ? glRegistry : new Dictionary<MeshRegistryKey, Mesh>();

        var regKey = new MeshRegistryKey(mesh, renderer);
        if (glRegistry.ContainsKey(regKey))
        {
            UnityEngine.Debug.Log("contains");
            return glRegistry[regKey];
        }

        string assetFolder = savePersistent ? PipelineSettings.PipelinePersistentFolder : PipelineSettings.PipelineAssetsFolder;
        if (!Directory.Exists(assetFolder))
        {
            Directory.CreateDirectory(assetFolder);
        }
        string nuMeshPath = assetFolder.Replace(Application.dataPath, "Assets") + renderer.transform.name + "_" + System.DateTime.Now.Ticks + ".asset";
        UnityEngine.Mesh nuMesh = UnityEngine.Object.Instantiate(mesh);

        AssetDatabase.CreateAsset(nuMesh, nuMeshPath);
        glRegistry[regKey] = nuMesh;
        return nuMesh;
    }

    Dictionary<UnityEngine.Mesh, UnityEngine.Mesh> glLinks;
    Dictionary<MeshRegistryKey, UnityEngine.Mesh> glRegistry;
    private void CreateUVBakedMeshes(bool savePersistent = false)
    {
        var renderers = Renderers.Where((renderer) => renderer.gameObject.activeInHierarchy && renderer.enabled);
        foreach(var renderer in renderers)
        {
            UnityEngine.Debug.LogWarning("here materials");
            UnityEngine.Debug.Log(renderer.sharedMaterial);
            UnityEngine.Debug.Log(renderer.sharedMaterial.name);
            UnityEngine.Debug.Log(renderer.sharedMaterials.Length);
            bool isSkinned = renderer.GetType() == typeof(SkinnedMeshRenderer);
            Mesh mesh = null;
            if (isSkinned)
            {
                mesh = ((SkinnedMeshRenderer)renderer).sharedMesh;
            }
            else
            {
                var filt = renderer.GetComponent<MeshFilter>();
                if (filt != null)
                    mesh = filt.sharedMesh;
            }
            if (mesh == null) continue;
            
            bool hasLightmap = WebaUnity.HasLightmap(renderer);
            bool hasTxrOffset = renderer.sharedMaterial != null && 
                (renderer.sharedMaterial.mainTextureOffset != Vector2.one ||
                    renderer.sharedMaterial.mainTextureScale != Vector2.one );
            
            if ((hasLightmap && PipelineSettings.lightmapMode == LightmapMode.BAKE_SEPARATE) ||
                    hasTxrOffset ||
                    Regex.IsMatch(AssetDatabase.GetAssetPath(mesh), @".*\.[(glb)(fbx)]"))
            {

                var nuMesh = GenerateMesh(renderer, mesh, savePersistent);
                UnityEngine.Debug.Log(nuMesh);
                
                if (hasLightmap)
                {

                    
                    UnityEngine.Debug.Log("has lightmap");

                    //var nuUv2s = nuMesh.uv2.Select((uv2) => uv2 * new Vector2(off.x, off.y) + new Vector2(off.z, off.w)).ToArray();
                    // var nuUv2s = nuMesh.uv2.Select((uv2) => new Vector2(uv2.x,1-uv2.y) * new Vector2(-off.x, -off.y) + new Vector2(off.z, off.w)).ToArray();
                    //var nuUv2s = nuMesh.uv2.Select((uv2) => new Vector2(1f-uv2.x, 1f-uv2.y));.ToArray();
                    var off = renderer.lightmapScaleOffset;
                    Vector2[] nuvs2 = new Vector2[nuMesh.uv2.Length];
                    for (int i = 0; i < nuvs2.Length; i++)
                    {
                        float valx = nuMesh.uv2[i].x;
                        float valy = nuMesh.uv2[i].y;

                        valx = (valx * off.x) + off.z;
                        //valy = (-(valy * off.y) - off.w) + 1f; // IN GLTF UVS IN Y AR INVERSED, THEY GO FROM LEFT TOP CORNER TO BOTTOM RIGHT CORNER, Y MUST BE INVERSED
                        valy = (valy * off.y) + off.w; // IN GLTF UVS IN Y AR INVERSED, THEY GO FROM LEFT TOP CORNER TO BOTTOM RIGHT CORNER, Y MUST BE INVERSED

                        nuvs2[i] = new Vector2(valx, valy);
                    }
                    nuMesh.uv2 = nuvs2;
                    nuMesh.UploadMeshData(false);
                }

                if (hasTxrOffset)
                {
                    var mat = renderer.sharedMaterial;
                    var off = mat.mainTextureOffset;
                    var scale = mat.mainTextureScale;
                    var nuUvs = nuMesh.uv.Select((uv) => uv * new Vector2(scale.x, scale.y) + new Vector2(off.x, off.y)).ToArray();
                    nuMesh.uv = nuUvs;
                    nuMesh.UploadMeshData(false);
                }

                if (!isSkinned)
                    renderer.GetComponent<MeshFilter>().sharedMesh = nuMesh;
                else
                {
                    ((SkinnedMeshRenderer)renderer).sharedMesh = nuMesh;
                }
                glLinks[nuMesh] = mesh;
            }
        }
        AssetDatabase.Refresh();
    }

    MeshBakeResult[] bakeResults;
    private void CombineMeshes(bool savePersistent = false)
    {
        var stagers = FindObjectsOfType<MeshBake>();
#if USING_MESHBAKER
        bakeResults = stagers.Select((baker) => baker.Bake(savePersistent)).Where((x) => x != null).ToArray();
        foreach(var result in bakeResults)
        {
            foreach(var original in result.originals)
            {
                original.GetComponent<MeshRenderer>().enabled = false;
            }
        }
        AssetDatabase.Refresh();
#endif
    }

    private void CleanupMeshCombine()
    {
        if(bakeResults != null)
        {
            foreach (var result in bakeResults)
            {
                foreach(var original in result.originals)
                {
                    if(original && original.GetComponent<MeshRenderer>())
                        original.GetComponent<MeshRenderer>().enabled = true;
                }
                foreach(var combined in result.combined)
                {
                    if(combined != null)
                        DestroyImmediate(combined.gameObject);
                }
            }
        }
        //MeshStager.ResetAll();
    }

    private Mesh RestoreGLLink(Mesh mesh, Renderer renderer)
    {
        if (renderer.GetType() == typeof(SkinnedMeshRenderer))
        {
            if (mesh != null &&
                glLinks.ContainsKey(mesh))
            {
                var originalMesh = glLinks[mesh];
                ((SkinnedMeshRenderer)renderer).sharedMesh = originalMesh;
                glRegistry.Remove(new MeshRegistryKey(originalMesh, renderer));
            }
        }
        else
        {
            if(mesh != null && 
                glLinks.ContainsKey(mesh))
            {
                var originalMesh = glLinks[mesh];
                renderer.GetComponent<MeshFilter>().sharedMesh = originalMesh;
                glRegistry.Remove(new MeshRegistryKey(originalMesh, renderer));
            }
        }
        return mesh;
    }

    private void RestoreGLLinks(IEnumerable<Renderer> rends)
    {
        List<Mesh> toRemove = new List<Mesh>();

        foreach (var rend in rends)
        {
            Mesh mesh = null;
            if (rend.GetType() == typeof(SkinnedMeshRenderer))
            {
                mesh = ((SkinnedMeshRenderer)rend).sharedMesh;
            } else
            {
                mesh = rend.GetComponent<MeshFilter>().sharedMesh;
            }
            if (mesh)
            {
                toRemove.Add(RestoreGLLink(mesh, rend));
            }
        }
        foreach(var doneMesh in toRemove)
        {
            glLinks.Remove(doneMesh);
        }
    }

    private void RestoreAllGLLinks()
    {
        if (glLinks != null)
        {
            RestoreGLLinks(Renderers);
        }
        glLinks = null;
        glRegistry = null;
    }
#endregion

    #region MESH INSTANCING
    InstanceMeshNode[] iNodes;
    public void FormatMeshInstancing()
    {
        iNodes = InstanceMeshNode.GenerateMeshNodes();
        if(iNodes != null)
        {
            foreach(InstanceMeshNode node in iNodes)
            {
                foreach(Transform xform in node.xforms)
                {
                    xform.gameObject.SetActive(false);
                    //xform.GetComponent<MeshRenderer>().enabled = false;
                }
            }
        }
    }

    public void CleanupMeshInstancing()
    {
        InstanceMeshNode.CleanupGeneratedMeshNodes();
        if (iNodes != null)
        {
            foreach(InstanceMeshNode node in iNodes)
            {
                foreach(Transform xform in node.xforms)
                {
                    //xform.GetComponent<MeshRenderer>().enabled = true;
                    xform.gameObject.SetActive(true);
                }
                DestroyImmediate(node.gameObject);
            }
        }
        iNodes = null;
    }
#endregion

    #region COLLIDERS
    /// <summary>
    /// Formats the scene to correctly export colliders to match Webaverse colliders spec
    /// </summary>
    GameObject cRoot;
    List<MeshRenderer> _disabled;
    private void FormatForExportingColliders()
    {
        _disabled = new List<MeshRenderer>();
        cRoot = new GameObject("Colliders", typeof(ColliderParent));
        //Dictionary<Collider, Transform> parents = new Dictionary<Collider, Transform>();
        Material defaultMat = AssetDatabase.LoadMainAssetAtPath(defaultMatPath) as Material;
        Collider[] colliders = GameObject.FindObjectsOfType<Collider>().Where((col) => col.gameObject.activeInHierarchy).ToArray();
        foreach(var collider in colliders)
        {
            Transform xform = collider.transform;
            Vector3 position = xform.position;
            Quaternion rotation = xform.rotation;
            Vector3 scale = xform.lossyScale;
            //parents[collider] = xform;
            if (collider.GetType() == typeof(BoxCollider))
            {
                var box = (BoxCollider)collider;
                GameObject clone = GameObject.CreatePrimitive(PrimitiveType.Cube);
                clone.name = xform.gameObject.name + "__COLLIDER__";
                clone.transform.position = position;
                clone.transform.rotation = rotation;
                clone.transform.localScale = scale;



                clone.transform.position += clone.transform.localToWorldMatrix.MultiplyVector(box.center);
                Vector3 nuScale = clone.transform.localScale;
                nuScale.x *= box.size.x;
                nuScale.y *= box.size.y;
                nuScale.z *= box.size.z;
                nuScale *= 0.5f;
                clone.transform.localScale = nuScale;
                MeshRenderer rend = clone.GetComponent<MeshRenderer>();
                //rend.lightmapIndex = -1;
                rend.material = defaultMat;
                clone.transform.SetParent(cRoot.transform, true);

                MeshRenderer thisRend = collider.GetComponent<MeshRenderer>();
                thisRend.enabled = false;
                _disabled.Add(thisRend);
            }
            else
            {
                GameObject clone = Instantiate(xform.gameObject, cRoot.transform, true);
                clone.transform.position = position;
                clone.transform.rotation = rotation;
                clone.name += "__COLLIDER__";
                MeshRenderer rend = clone.GetComponent<MeshRenderer>();
                rend.enabled = true;
                rend.lightmapIndex = -1;
            }
        }
    }
    private void CleanUpExportingColliders()
    {
        if(_disabled != null)
        {
            foreach(var rend in _disabled)
            {
                rend.enabled = true;
            }
            _disabled = null;
        }
        if(cRoot)
        {
            DestroyImmediate(cRoot);
        }
    }

#endregion

    private void Export(bool savePersistent, bool fullScene = true)
    {
        ExportSequence(savePersistent, fullScene);
    }
    private void ExportSequence(bool savePersistent, bool fullScene = true)
    {
        // FOLDER SETUP
        // DEV
        // STEP 1 MAKE SURE PIPELINE HAS BASIC FOLDERS
        PipelineSettings.CreateMissingDirectories();

        // PENDING TO CHECK WHAT IS THIS FOLDER USED FOR
        string exportFolder = Path.Combine(PipelineSettings.ProjectFolder, "assets");
        DirectoryInfo outDir = new DirectoryInfo(exportFolder);
        if(!outDir.Exists)
        {
            Directory.CreateDirectory(exportFolder);
        }

        // CLEAR EXISTING CONVERSION DATA
        PipelineSettings.ClearConversionData();

        //set exporter path
        ExporterSettings.Export.name = PipelineSettings.GLTFName;
        ExporterSettings.Export.folder = PipelineSettings.ConversionFolder;
        //set other exporter parameters
        ExporterSettings.NormalTexture.maxSize = PipelineSettings.CombinedTextureResolution;
        //END FOLDER SETUP 

        //TODO: move most of these export helper functions into the OnExport handlers of the
        //      Webaverse classes

        StageLights();
        StageRealtimeLights();
        StageLightmapping();
        FormatForExportingLODs();

        if(PipelineSettings.ExportColliders)
        {
            FormatForExportingColliders();
        }

        if (PipelineSettings.InstanceMeshes && PipelineSettings.BasicGLTFConvert)
        {
            FormatMeshInstancing();
        }

        SerializeAllMaterials();

        CreateBakedMeshes(savePersistent);
        
        if (PipelineSettings.ExportSkybox)
        {
            FormatForExportingSkybox();
        }

        if(PipelineSettings.ExportEnvmap)
        {
            FormatForExportingEnvmap();
        }

        

        //convert materials to WebaPBR
        StandardToWebaPBR.AllToWebaPBR();

        try
        {
            exporter.Export(fullScene);
        } catch (System.NullReferenceException e)
        {
            UnityEngine.Debug.LogError("export error:" + e);
        }

        state = State.POST_EXPORT;

        if (PipelineSettings.ExportColliders)
        {
            CleanUpExportingColliders();
        }

        if (PipelineSettings.InstanceMeshes && PipelineSettings.BasicGLTFConvert)
        {
            CleanupMeshInstancing();
        }
        //restore materials
        StandardToWebaPBR.RestoreMaterials();
        if (PipelineSettings.meshMode == MeshExportMode.COMBINE)
        {
            CleanupMeshCombine();
        }

        RestoreAllGLLinks();
        DeserializeAllMaterials();
        CleanupExportingLODs();

        if(PipelineSettings.ExportSkybox)
        {
            CleanupExportingSkybox();
        }

        if(PipelineSettings.ExportEnvmap)
        {
            CleanupExportEnvmap();
        }

        PipelineSettings.ClearPipelineJunk();
        //UnityEngine.Debug.Log("ExportPath " + ExportPath);

        var converter = new GLTFToGLBConverter();
        converter.ConvertToGLB(PipelineSettings.ConversionFolder + PipelineSettings.GLTFName);
        var GLBName = PipelineSettings.ConversionFolder + PipelineSettings.GLTFName + ".glb";

        SendToWebaverse(GLBName);
        CreateMetaverseFile();
        CreateSceneFile(GLBName);

        //File.Delete(Path.Combine(PipelineSettings.ConversionFolder, PipelineSettings.GLTFName + ".glb"));
        //File.Delete(Path.Combine(PipelineSettings.ConversionFolder, PipelineSettings.GLTFName + ".gltf"));
        //File.Delete(Path.Combine(PipelineSettings.ConversionFolder, PipelineSettings.GLTFName + ".bin"));

        CleanupLightmapping();
        CleanupLights();
        CleanupRealtimeLights();
        state = State.INITIAL;


    }

    private void SendToWebaverse(string GLBName)
    {
        // if project folder directory doesn't exist, create it
        DirectoryInfo projectDir = new DirectoryInfo(PipelineSettings.ProjectFolder);
        if (!projectDir.Exists)
            Directory.CreateDirectory(PipelineSettings.ProjectFolder);

        // if project folder directory doesn't exist, create it
        projectDir = new DirectoryInfo(PipelineSettings.ProjectFolder + "/Webaverse");
        if (!projectDir.Exists)
            Directory.CreateDirectory(PipelineSettings.ProjectFolder + "/Webaverse");

        projectDir = new DirectoryInfo(PipelineSettings.ProjectFolder + "/Webaverse/" + PipelineSettings.GLTFName);
        if (!projectDir.Exists)
            Directory.CreateDirectory(PipelineSettings.ProjectFolder + "/Webaverse/" + PipelineSettings.GLTFName);

        // copy the glb to the project folder
        File.Copy(GLBName, Path.Combine(PipelineSettings.ProjectFolder + "/Webaverse", PipelineSettings.GLTFName, PipelineSettings.GLTFName + ".glb"), true);
    }

    private void CreateMetaverseFile()
    {
        String metaverseFile = "{\"name\": \"" + PipelineSettings.GLTFName + "\", \"start_url\": \"" + PipelineSettings.GLTFName + ".scn\" }";
        // write the metaverse file to the project folder
        File.WriteAllText(Path.Combine(PipelineSettings.ProjectFolder + "/Webaverse", PipelineSettings.GLTFName, ".metaversefile"), metaverseFile);
    }

    private void CreateSceneFile(string GLBName)
    {
        SceneFile scene = new SceneFile();

        var atmosphereObject = new SceneObject();
        atmosphereObject.position = new float[3] { 0, 0, 0 };
        atmosphereObject.start_url = "https://webaverse.github.io/atmospheric-sky/";
        scene.objects.Add(atmosphereObject);

        var sceneObject = new SceneObject();
        sceneObject.position = new float[3] { 0, 0, 0 };
        sceneObject.start_url = "./" + PipelineSettings.GLTFName + ".glb";
        scene.objects.Add(sceneObject);
        UnityEngine.Debug.Log("realtimeLights" + realtimeLights);

        foreach (var light in realtimeLights)
        {
            UnityEngine.Debug.Log("Handling light loop...");
            UnityEngine.Debug.Log(light.ToString());
            SceneObject lightObject = new SceneObject();
            lightObject.type = "application/light";

            SceneObjectContent content = new SceneObjectContent();
            lightObject.content = content;

            content.lightType = light.type.ToString();

            // Set color
            content.args = "[[" + MathF.Floor(light.color.r * 255) + ", " +
            MathF.Floor(light.color.g * 255) + ", " +
            MathF.Floor(light.color.b * 255) + "], " +
            MathF.Floor(light.intensity * 255) + "]";

            content.shadow = "[150, 5120, 0.1, 10000, -0.0001]";

            Transform t = light.GetComponent<Transform>();

            content.position = new float[3] { t.position.x, t.position.y, t.position.z };
            content.quaternion = new float[4] { t.rotation.x, t.rotation.y, t.rotation.z, t.rotation.w };
            content.scale = new float[3] { t.localScale.x, t.localScale.y, t.localScale.z };

            scene.objects.Add(lightObject);
        }

        var setting = new JsonSerializerSettings();
        setting.Formatting = Formatting.Indented;
        setting.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
        setting.NullValueHandling = NullValueHandling.Ignore;

        var json = JsonConvert.SerializeObject(scene, setting);
        var path = Path.Combine(PipelineSettings.ProjectFolder + "/./Webaverse", PipelineSettings.GLTFName, PipelineSettings.GLTFName + ".scn");

        File.WriteAllText(path, json);
    }
}

public class SceneFile
{
    public List<SceneObject> objects = new List<SceneObject>();
}

public class SceneObject
{
    public string type;
    public string start_url;
    public float[] position;
    public float[] quaternion;
    public float[] scale;
    bool? _dynamic;
    bool? _physics;

    public SceneObjectContent content;
}

public class SceneObjectContent
{
    public string lightType;
    public string args;
    public string shadow;
    public float[] position;
    public float[] quaternion;
    public float[] scale;
}