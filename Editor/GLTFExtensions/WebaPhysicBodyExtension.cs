﻿using GLTF.Math;
using GLTF.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine;

public class WebaPhysicBodyExtension : Extension
{
    public WebaRigidBody rigidBody;
    public List<Collider> colliders = new List<Collider>();
    public GameObject go;

    public JProperty Serialize()
    {
        if (go.GetComponent<WebaRigidBody>() == null)
        {
            rigidBody = go.AddComponent<WebaRigidBody>();
            rigidBody.sleeping = true;
        }

        var value = new JObject(
            new JProperty("mass", rigidBody.mass),
            new JProperty("friction", rigidBody.friction),
            new JProperty("restitution", rigidBody.restitution),
            new JProperty("unControl", rigidBody.unControl),
            new JProperty("physicStatic", rigidBody.physicStatic),
            new JProperty("sleeping", rigidBody.sleeping)
        );

        if (colliders.Count > 0)
        {
            value.Add("colliders", new JArray());
        }

        int i = 0;
        foreach (var collider in colliders)
        {
            var tmp = new JObject();
            ((JArray)value["colliders"]).Add(tmp);

            if (collider is SphereCollider)
            {
                var center = ((SphereCollider)collider).center;
                tmp.Add("name", "sphere" + i);
                tmp.Add("type", "SPHERE");
                tmp.Add("offset", new JArray { center.x, center.y, center.z });
                tmp.Add("radius", ((SphereCollider)collider).radius); 
            }
            else if (collider is BoxCollider)
            {
                var center = ((BoxCollider)collider).center;
                var size = ((BoxCollider)collider).size;
                tmp.Add("name", "box" + i);
                tmp.Add("type", "BOX");
                tmp.Add("offset", new JArray { center.x, center.y, center.z });
                tmp.Add("size", new JArray { size.x, size.y, size.z });
            }
            else
            {
                Debug.LogWarning("In current time, Weba only supports shpere and box collider !");
            }
            // todo: heightmap collider
            //else if (collider is TerrainCollider)
            //{

            //}
            // todo: capsule collider

            tmp.Add("isTrigger", collider.isTrigger);

            i += 1;
        }

        return new JProperty(ExtensionManager.GetExtensionName(typeof(WebaPhysicBodyExtensionFactory)), value);
    }
}