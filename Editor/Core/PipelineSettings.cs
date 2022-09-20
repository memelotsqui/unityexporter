using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

using System.IO;
using System.Text.RegularExpressions;

using Newtonsoft.Json;
using System.Linq;

public enum LightmapMode
{
    IGNORE,
    BAKE_COMBINED,
    BAKE_SEPARATE
}

public enum MeshExportMode
{
    DEFAULT,
    COMBINE,
    NO_MESHES
}

public enum ExportFormat
{
    GLB,
    GLTF
}

[InitializeOnLoad]
public static class PipelineSettings
{

    [System.Serializable]
    public struct Data
    {
        public string GLTFName;
        public string ProjectFolder;
        public bool ExportLights;
        public bool ExportColliders;
        public bool ExportSkybox;
        public bool ExportEnvmap;

        public bool BasicGLTFConvert;

        public bool InstanceMeshes;
        public bool MeshOptCompression;
        public bool MeshQuantization;
        public bool KTX2Compression;
        public bool CombineMaterials;
        public bool CombineNodes;

        public ExportFormat format;

        public MeshExportMode meshMode;
        public bool preserveLightmapping;
        public LightmapMode lightmapMode;
        public int CombinedTextureResolution;
        public void Apply()
        {
            PipelineSettings.GLTFName = GLTFName;
            PipelineSettings.ProjectFolder = this.ProjectFolder;

            PipelineSettings.ExportLights = this.ExportLights;
            PipelineSettings.ExportColliders = this.ExportColliders;
            PipelineSettings.ExportSkybox = this.ExportSkybox;
            PipelineSettings.ExportEnvmap = ExportEnvmap;

            PipelineSettings.ExportFormat = format;

            PipelineSettings.BasicGLTFConvert = BasicGLTFConvert;

            PipelineSettings.InstanceMeshes = this.InstanceMeshes;
            PipelineSettings.MeshOptCompression = this.MeshOptCompression;
            PipelineSettings.MeshQuantization = this.MeshQuantization;
            PipelineSettings.KTX2Compression = this.KTX2Compression;
            PipelineSettings.CombineMaterials = this.CombineMaterials;
            PipelineSettings.CombineNodes = this.CombineNodes;

            PipelineSettings.lightmapMode = this.lightmapMode;
            PipelineSettings.preserveLightmapping = this.preserveLightmapping;
            PipelineSettings.meshMode = meshMode;

            PipelineSettings.CombinedTextureResolution = this.CombinedTextureResolution;
        }
        public void Set()
        {
            GLTFName = PipelineSettings.GLTFName;
            ProjectFolder = PipelineSettings.ProjectFolder;
            
            ExportLights = PipelineSettings.ExportLights;
            ExportColliders = PipelineSettings.ExportColliders;
            ExportSkybox = PipelineSettings.ExportSkybox;
            ExportEnvmap = PipelineSettings.ExportEnvmap;

            format = PipelineSettings.ExportFormat;
            BasicGLTFConvert = PipelineSettings.BasicGLTFConvert;

            InstanceMeshes = PipelineSettings.InstanceMeshes;
            MeshOptCompression = PipelineSettings.MeshOptCompression;
            MeshQuantization = PipelineSettings.MeshQuantization;
            KTX2Compression = PipelineSettings.KTX2Compression;
            CombineMaterials = PipelineSettings.CombineMaterials;
            CombineNodes = PipelineSettings.CombineNodes;

            lightmapMode = PipelineSettings.lightmapMode;
            meshMode = PipelineSettings.meshMode;

            preserveLightmapping = PipelineSettings.preserveLightmapping;
            CombinedTextureResolution = PipelineSettings.CombinedTextureResolution;
        }
    }

    public static readonly string ConversionFolder = Application.dataPath + "/../Outputs/GLTF/";
    public static readonly string PipelineFolder = Application.dataPath + "/../Pipeline/";
    static string _configBasePath = PipelineFolder + "settings_";
    public static string ConfigFile
    {
        get
        {
            return _configBasePath + EditorSceneManager.GetActiveScene().name + ".conf";
        }
    }
    public static readonly string BuiltinAssetsFolder = "/Webaverse/Builtin/";
    public static readonly string PipelineAssetsFolder = Application.dataPath + "/Webaverse/PipelineAssets/";
    public static readonly string PipelinePersistentFolder = Application.dataPath + "/Webaverse/PersistentAssets/";
    public static string GLTFName;
    public static string ProjectFolder;// = Application.dataPath + "/../Outputs/GLB/";

    

    public static string ProjectName => 
        Regex.Match(ProjectFolder, @"(?<=[\\\/])[\w-_]+(?=[\\\/]*$)").Value;

    public static string LocalPath => "https://localhost:8642/" + 
        Regex.Match(ProjectFolder, @"[\w-]+[\\/]+[\w-]+[\\/]*$").Value;

    public static string ScriptsFolder => ProjectFolder + "/assets/scripts/";
    public static bool ExportLights;
    public static bool ExportColliders;
    public static bool ExportSkybox;
    public static bool ExportEnvmap;

    public static bool BasicGLTFConvert;

    public static bool InstanceMeshes;
    public static bool MeshOptCompression;
    public static bool MeshQuantization;
    public static bool KTX2Compression;
    public static bool CombineMaterials;
    public static bool CombineNodes;

    public static ExportFormat ExportFormat;

    public static MeshExportMode meshMode;

    public static bool preserveLightmapping;

    public static LightmapMode lightmapMode;

    public static int CombinedTextureResolution;// = 8192;
    

    static PipelineSettings()
    {
        Initialize();
        EditorSceneManager.activeSceneChangedInEditMode += (_old, _new) => Initialize();
    }

    static void Initialize()
    {
        if (!ReadSettingsFromConfig())
        {
            CombinedTextureResolution = 2048;
        }
    }

    public static bool ReadSettingsFromConfig()
    {
        var config = new FileInfo(ConfigFile);
        if (!config.Exists)
        {
            return false;
        }
        var data = JsonConvert.DeserializeObject<Data>
        (
            File.ReadAllText(ConfigFile)
        );
        data.Apply();
        return true;
    }

    public static void SaveSettings()
    {
        var data = new Data();
        data.Set();
        CreateMissingDirectories();
        File.WriteAllText(ConfigFile, JsonConvert.SerializeObject(data, Formatting.Indented));
    }
    public static void CreateBuiltinFolder()
    {
        if (!Directory.Exists(Application.dataPath + BuiltinAssetsFolder))
        {
            Directory.CreateDirectory(Application.dataPath + BuiltinAssetsFolder);
        }
    } 
    public static void CreateMissingDirectories()
    {
        DirectoryInfo pipelineDirectory = new DirectoryInfo(PipelineFolder);
        if (!pipelineDirectory.Exists)
        {
            Directory.CreateDirectory(PipelineFolder);
        }
        DirectoryInfo conversionDirectory = new DirectoryInfo(ConversionFolder);
        if (!conversionDirectory.Exists)
        {
            Directory.CreateDirectory(ConversionFolder);
        }
    }
    public static void ClearConversionData()
    {
        DirectoryInfo conversionDirectory = new DirectoryInfo(ConversionFolder);
        if (conversionDirectory.Exists)
        {
            var files = conversionDirectory.GetFiles();
            var subDirectories = conversionDirectory.GetDirectories();

            // delete files in pipeline folder to make way for new export
            foreach (var file in files)
            {
                file.Delete();
            }

            foreach (var subDir in subDirectories)
            {
                subDir.Delete(true);
            }
        }
    }

    public static void ClearPipelineJunk()
    {
        if (Directory.Exists(PipelineFolder))
        {
            Regex filter = new Regex(@".*\.(jpg|png|tga|asset|mat)");

            var pipelineFiles = Directory.GetFiles(PipelineFolder);
            Queue<string> q = new Queue<string>(pipelineFiles);
            List<string> toRemove = new List<string>();
            while (q.Count > 0)
            {
                string path = q.Dequeue();
                if (Directory.Exists(path))
                {
                    string[] contents = Directory.GetFiles(path);
                    contents.ToList().ForEach((x) => q.Enqueue(x));
                }
                else
                if (filter.IsMatch(path))
                {
                    toRemove.Add(path);
                }
            }
            AssetDatabase.DeleteAssets(toRemove.ToArray(), new List<string>());
        }
        else
        {
            Debug.Log("Pipeline folder clear");
        }
        AssetDatabase.DeleteAsset(PipelineAssetsFolder.Replace(Application.dataPath, "Assets"));
    }
}

