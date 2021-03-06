﻿// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Xml.Serialization;
using CL = Collada141;
using LibreLancer.Vertices;
using LibreLancer.Utf.Vms;
namespace LibreLancer.ContentEdit
{
    public class ColladaObject
    {
        public string Name;
        public string ID;
        public Matrix4x4 Transform;
        public ColladaGeometry Geometry;
        public ColladaSpline Spline;
        public List<ColladaObject> Children = new List<ColladaObject>();
        public bool AutodetectInclude;
    }
    public struct ColladaDrawcall
    {
        public int StartIndex;
        public int TriCount;
        public int StartVertex;
        public int EndVertex;
        public ColladaMaterial Material;
    }
    public class ColladaMaterial
    {
        public string Name;
        public Color4 Dc;
    }
    public class ColladaSpline
    {
        
    }
    public class ColladaGeometry 
    {
        public float Radius;
        public Vector3 Center;
        public Vector3 Min;
        public Vector3 Max;
        public ushort[] Indices;
        public VertexPositionNormalDiffuseTextureTwo[] Vertices;
        public ColladaDrawcall[] Drawcalls;
        public D3DFVF FVF;
        public string Name;

        public void CalculateDimensions()
        {
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            float avgX = 0, avgY = 0, avgZ = 0;
            foreach (var v in Vertices)
            {
                minX = Math.Min(minX, v.Position.X);
                minY = Math.Min(minY, v.Position.Y);
                minZ = Math.Min(minZ, v.Position.Z);

                maxX = Math.Max(maxX, v.Position.X);
                maxY = Math.Max(maxY, v.Position.Y);
                maxZ = Math.Max(maxZ, v.Position.Z);

                avgX += v.Position.X;
                avgY += v.Position.Y;
                avgZ += v.Position.Z;
            }
            Min = new Vector3(minX, minY, minZ);
            Max = new Vector3(maxX, maxY, maxZ);
            Center = new Vector3(avgX, avgY, avgZ) / Vertices.Length;
            Radius = Math.Max(
                Vector3.Distance(Center, Min),
                Vector3.Distance(Center, Max)
            );
        }
        public byte[] VMeshRef(string nodename)
        {
            using(var stream = new MemoryStream())
            {
                var writer = new BinaryWriter(stream);

                writer.Write((uint)60); //HeaderSize
                writer.Write(CrcTool.FLModelCrc(nodename));
                //Fields used for referencing sections of VMeshData
                writer.Write((ushort)0); //StartVertex - BaseVertex in drawcall
                writer.Write((ushort)Vertices.Length); //VertexCount (idk?)
                writer.Write((ushort)0); //StartIndex
                writer.Write((ushort)Indices.Length); //IndexCount
                writer.Write((ushort)0); //StartMesh
                writer.Write((ushort)Drawcalls.Length); //MeshCount
                //Write rendering things
                writer.Write(Max.X);
                writer.Write(Min.X);
                writer.Write(Max.Y);
                writer.Write(Min.Y);
                writer.Write(Max.Z);
                writer.Write(Min.Z);


                writer.Write(Center.X);
                writer.Write(Center.Y);
                writer.Write(Center.Z);

                writer.Write(Radius);
                return stream.ToArray();
            }
        }
        public byte[] VMeshData()
        {
            using (var stream = new MemoryStream())
            {
                var writer = new BinaryWriter(stream);
                writer.Write((uint)0x01); //MeshType
                writer.Write((uint)0x04); //SurfaceType
                writer.Write((ushort)(Drawcalls.Length)); //MeshCount
                writer.Write((ushort)(Indices.Length)); //IndexCount
                writer.Write((ushort)FVF); //FVF
                writer.Write((ushort)Vertices.Length); //VertexCount

                int startTri = 0;
                foreach(var dc in Drawcalls) {
                    //drawcalls must be sequential (start index isn't in VMeshData)
                    //this error shouldn't ever throw
                    if (startTri != dc.StartIndex) throw new Exception("Invalid start index");
                    //write TMeshHeader
                    var crc = dc.Material != null ? CrcTool.FLModelCrc(dc.Material.Name) : 0;
                    writer.Write(crc);
                    writer.Write((ushort)dc.StartVertex);
                    writer.Write((ushort)dc.EndVertex);
                    writer.Write((ushort)(dc.TriCount * 3)); //NumRefVertices
                    writer.Write((ushort)0); //Padding
                    //validation
                    startTri += dc.TriCount * 3;
                }

                foreach (var idx in Indices) writer.Write(idx);
                foreach(var v in Vertices) {
                    writer.Write(v.Position.X);
                    writer.Write(v.Position.Y);
                    writer.Write(v.Position.Z);
                    if((FVF & D3DFVF.NORMAL) == D3DFVF.NORMAL) {
                        writer.Write(v.Normal.X);
                        writer.Write(v.Normal.Y);
                        writer.Write(v.Normal.Z);
                    }
                    if ((FVF & D3DFVF.DIFFUSE) == D3DFVF.DIFFUSE) {
                        writer.Write(v.Diffuse);
                    }
                    //Librelancer stores texture coordinates flipped internally
                    if((FVF & D3DFVF.TEX2) == D3DFVF.TEX2) {
                        writer.Write(v.TextureCoordinate.X);
                        writer.Write(1 - v.TextureCoordinate.Y);
                        writer.Write(v.TextureCoordinateTwo.X);
                        writer.Write(1 - v.TextureCoordinateTwo.Y);
                    } else if ((FVF & D3DFVF.TEX1) == D3DFVF.TEX1) {
                        writer.Write(v.TextureCoordinate.X);
                        writer.Write(1 - v.TextureCoordinate.Y);
                    }
                }
                return stream.ToArray();
            }
        }
    }

    public class ColladaSupport
    {
        //This is explicitly initialised in a background thread
        //Because instantiating this object takes >1 second
        public static XmlSerializer XML
        {
            get
            {
                if (!initing) throw new InvalidOperationException("Not inited");
                while(!inited) Thread.Sleep(0);
                return _xml;
            }
        }
        private static XmlSerializer _xml;
        private static bool initing = false;
        private static volatile bool inited = false;
        public static void InitXML()
        {
            if (initing) return;
            initing = true;
            new Thread(() =>
            {
                _xml = new XmlSerializer(typeof(CL.COLLADA));
                FLLog.Info("Collada", "Xml support loaded");
                inited = true; 
            }).Start();
        }
        public static List<ColladaObject> Parse(string filename)
        {
            CL.COLLADA dae;
            using(var reader =new StreamReader(filename)) {
                dae = (CL.COLLADA)XML.Deserialize(reader);
            }
            //Get libraries
            var geometrylib = dae.Items.OfType<CL.library_geometries>().First();
            var scenelib = dae.Items.OfType<CL.library_visual_scenes>().First();
            var matlib = dae.Items.FirstOfType<CL.library_materials>();
            var fxlib = dae.Items.FirstOfType<CL.library_effects>();
            //Get main scene
            var urlscn = CheckURI(dae.scene.instance_visual_scene.url);
            var scene = scenelib.visual_scene.Where((x) => x.id == urlscn).First();
            //Walk through objects
            var up = dae.asset.up_axis;
            var objs = new List<ColladaObject>();
            foreach(var node in scene.node) {
                objs.Add(ProcessNode(up, geometrylib, node,matlib, fxlib));
            }
            return objs;
        }

        static ColladaObject ProcessNode(CL.UpAxisType up, CL.library_geometries geom, CL.node n, CL.library_materials matlib, CL.library_effects fxlib)
        {
            var obj = new ColladaObject();
            obj.Name = n.name;
            obj.ID = n.id;
            if(n.instance_geometry != null && n.instance_geometry.Length > 0) {
                //Geometry object
                if (n.instance_geometry.Length != 1) throw new Exception("How to handle multiple geometries/node?");
                var uri = CheckURI(n.instance_geometry[0].url);
                var g = geom.geometry.Where((x) => x.id == uri).First();
                if(g.Item is CL.mesh) {
                    obj.Geometry = GetGeometry(up, g, matlib, fxlib);
                } else if (g.Item is CL.spline) {
                    obj.Spline = GetSpline(up, g);
                }
            }
            if(n.Items.OfType<CL.matrix>().Any()) {
                var tr = n.Items.OfType<CL.matrix>().First();
                obj.Transform = GetMatrix(up,tr.Text);
            } else {
                Matrix4x4 mat = Matrix4x4.Identity;
                foreach(var item in n.Items) {
                    if(item is CL.TargetableFloat3) {
                        //var float3 = 
                    }
                }
                obj.Transform = mat;
            }
            if(n.node1 != null && n.node1.Length > 0) {
                foreach(var node in n.node1) {
                    obj.Children.Add(ProcessNode(up, geom, node,matlib, fxlib));
                }
            }
            return obj;
        }
        static ColladaSpline GetSpline(CL.UpAxisType up, CL.geometry geo)
        {
            var spline = geo.Item as CL.spline;
            if (spline == null) return null;
            var conv = new ColladaSpline();
            Dictionary<string, float[]> arrays = new Dictionary<string, float[]>();
            Dictionary<string, GeometrySource> sources = new Dictionary<string, GeometrySource>();
            //Get arrays
            foreach (var acc in spline.source){
                var arr = acc.Item as CL.float_array;
                arrays.Add(arr.id, FloatArray(arr.Text));
            }
            //Accessors
            foreach (var acc in spline.source) {
                sources.Add(acc.id, new GeometrySource(acc, arrays));
            }
            //Process spline

            /*foreach(var input in spline.control_vertices.input) {
                switch(input.semantic) {
                    
                }
            }
            spline.*/
            return conv;
        }
        const string SEM_VERTEX = "VERTEX";
        const string SEM_POSITION = "POSITION";
        const string SEM_COLOR = "COLOR";
        const string SEM_NORMAL = "NORMAL";
        const string SEM_TEXCOORD = "TEXCOORD";

        static void SetDc(ColladaMaterial material, CL.common_color_or_texture_type obj)
        {
            if (obj == null) return;
            if (obj.Item is CL.common_color_or_texture_typeColor col)
            {
                TryParseColor(col.Text, out material.Dc);
            }
            if (obj.Item is CL.common_color_or_texture_typeTexture tex)
            {
                material.Dc = Color4.White;
                FLLog.Warning("Collada", "Not Implemented: Diffuse texture import");
            }
        }
      
        static ColladaMaterial ParseMaterial(string name, CL.library_materials matlib, CL.library_effects fxlib)
        {
            if(matlib == null) return new ColladaMaterial()
            {
                Dc = Color4.Red,
                Name = name
            };
            var src = matlib.material.FirstOrDefault(mat => mat.id.Equals(name, StringComparison.InvariantCulture));
            if (src == null)
                return new ColladaMaterial()
                {
                    Dc = Color4.Red,
                    Name = name
                };
            var output = new ColladaMaterial() {Dc = Color4.Red};
            output.Name = string.IsNullOrWhiteSpace(src.name) ? name : src.name;
            if (src.instance_effect == null) return output;
            if (fxlib == null) return output;
            var fx = fxlib.effect.FirstOrDefault(fx =>
                fx.id.Equals(CheckURI(src.instance_effect.url), StringComparison.InvariantCulture));
            if (fx != null)
            {
                var profile = fx.Items.FirstOfType<CL.effectFx_profile_abstractProfile_COMMON>();
                if (profile == null) return output;
                if (profile.technique == null) return output;
                switch (profile.technique.Item)
                {
                    case CL.effectFx_profile_abstractProfile_COMMONTechniquePhong phong:
                        SetDc(output, phong.diffuse);
                        break;
                    case CL.effectFx_profile_abstractProfile_COMMONTechniqueBlinn blinn:
                        SetDc(output, blinn.diffuse);
                        break;
                    case CL.effectFx_profile_abstractProfile_COMMONTechniqueConstant constant:
                        output.Dc = Color4.White;
                        break;
                    case CL.effectFx_profile_abstractProfile_COMMONTechniqueLambert lambert:
                        SetDc(output, lambert.diffuse);
                        break;
                }
            }
            return output;
        }
        static ColladaGeometry GetGeometry(CL.UpAxisType up, CL.geometry geo, CL.library_materials matlib, CL.library_effects fxlib)
        {
            var conv = new ColladaGeometry() { FVF = D3DFVF.XYZ };
            conv.Name = string.IsNullOrEmpty(geo.name) ? geo.id : geo.name;
            var msh = geo.Item as CL.mesh;
            if (msh == null) return null;
            List<VertexPositionNormalDiffuseTextureTwo> vertices = new List<VertexPositionNormalDiffuseTextureTwo>();
            List<int> hashes = new List<int>();
            List<ushort> indices = new List<ushort>();
            List<ColladaDrawcall> drawcalls = new List<ColladaDrawcall>();
            Dictionary<string, GeometrySource> sources = new Dictionary<string, GeometrySource>();
            Dictionary<string, float[]> arrays = new Dictionary<string, float[]>();
            Dictionary<string, GeometrySource> verticesRefs = new Dictionary<string, GeometrySource>();
            int placeHolderIdx = 0;
            //Get arrays
            foreach(var acc in msh.source) {
                var arr = acc.Item as CL.float_array;
                arrays.Add(arr.id, FloatArray(arr.Text));
            }
            //Accessors
            foreach(var acc in msh.source) {
                sources.Add(acc.id, new GeometrySource(acc, arrays));
            }
            //Process geometry
            foreach(var item in msh.Items) {
                if(!(item is CL.triangles || item is CL.polylist || item is CL.polygons)) {
                    FLLog.Warning("Collada", "Ignoring " + item.GetType().Name + " element.");
                }
            }
            int totalTriangles = 0;
            foreach(var item in msh.Items.Where(x => x is CL.triangles || x is CL.polylist || x is CL.polygons)) {
                if(item is CL.triangles)
                    totalTriangles += (int)((CL.triangles)item).count;
                else if (item is CL.polylist plist)
                        totalTriangles += (int)plist.count;
                else
                    totalTriangles += (int)((CL.polygons)item).count;
            }
            if(totalTriangles > 21845) {
                throw new Exception(string.Format(
                    "Overflow!\nCollada geometry {0} has {1} triangles\nVMeshData has limit of 21845",
                    string.IsNullOrEmpty(geo.name) ? geo.id : geo.name,
                    totalTriangles));
            }
            foreach(var item in msh.Items.Where(x => x is CL.triangles || x is CL.polylist || x is CL.polygons)) {
                CL.InputLocalOffset[] inputs;
                int[] pRefs;
                int indexCount;
                string materialRef;
                ColladaMaterial material;
                if(item is CL.triangles) {
                    var triangles = (CL.triangles)item;
                    indexCount = (int)(triangles.count * 3);
                    pRefs = IntArray(triangles.p);
                    inputs = triangles.input;
                    materialRef = triangles.material;
                } else if (item is CL.polygons polygons)
                {
                    indexCount = (int)(polygons.count * 3);
                    int j = 0;
                    pRefs = new int[indexCount];
                    foreach (var arr in polygons.Items)
                    {
                        if(!(arr is string)) throw new Exception("Polygons: ph element unsupported");
                        var ints = IntArray((string) arr);
                        if (ints.Length != 3)
                            throw new Exception("Polygons: non-triangle geometry not supported");
                        pRefs[j] = ints[0];
                        pRefs[j + 1] = ints[1];
                        pRefs[j + 2] = ints[2];
                        j += 3;
                    }
                    inputs = polygons.input;
                    materialRef = polygons.material;
                } else  {
                    var plist = (CL.polylist)item;
                    pRefs = IntArray(plist.p);
                    foreach(var c in IntArray(plist.vcount)) {
                        if(c != 3) {
                            throw new Exception("Polylist: non-triangle geometry");
                        }
                    }
                    materialRef = plist.material;
                    inputs = plist.input;
                    indexCount = (int)(plist.count * 3);
                }
                if (indexCount == 0) continue; //Skip empty
                material = ParseMaterial(materialRef, matlib, fxlib);
                int pStride = 0;
                foreach (var input in inputs)
                    pStride = Math.Max((int)input.offset, pStride);
                pStride++;
                GeometrySource sourceXYZ = null; int offXYZ = int.MinValue;
                GeometrySource sourceNORMAL = null; int offNORMAL = int.MinValue;
                GeometrySource sourceCOLOR = null; int offCOLOR = int.MinValue;
                GeometrySource sourceUV1 = null; int offUV1 = int.MinValue;
                GeometrySource sourceUV2 = null; int offUV2 = int.MinValue;
                int texCount = 0;
                int startIdx = indices.Count;
                foreach(var input in inputs) {
                    switch(input.semantic) {
                        case SEM_VERTEX:
                            if (CheckURI(input.source) != msh.vertices.id)
                                throw new Exception("VERTEX doesn't match mesh vertices");
                            foreach(var ip2 in msh.vertices.input) {
                                switch(ip2.semantic) {
                                    case SEM_POSITION:
                                        offXYZ = (int)input.offset;
                                        sourceXYZ = sources[CheckURI(ip2.source)];
                                        break;
                                    case SEM_NORMAL:
                                        offNORMAL = (int)input.offset;
                                        sourceNORMAL = sources[CheckURI(ip2.source)];
                                        conv.FVF |= D3DFVF.NORMAL;
                                        break;
                                    case SEM_COLOR:
                                        offCOLOR = (int)input.offset;
                                        sourceCOLOR = sources[CheckURI(ip2.source)];
                                        conv.FVF |= D3DFVF.DIFFUSE;
                                        break;
                                    case SEM_TEXCOORD:
                                        if (texCount == 2) throw new Exception("Too many texcoords!");
                                        if (texCount == 1)
                                        {
                                            offUV2 = (int)input.offset;
                                            sourceUV2 = sources[CheckURI(ip2.source)];
                                            conv.FVF &= ~D3DFVF.TEX1;
                                            conv.FVF |= D3DFVF.TEX2;
                                        }
                                        else
                                        {
                                            offUV1 = (int)input.offset;
                                            sourceUV1 = sources[CheckURI(ip2.source)];
                                            if ((conv.FVF & D3DFVF.TEX2) != D3DFVF.TEX2) conv.FVF |= D3DFVF.TEX1;
                                        }
                                        texCount++;
                                        break;
                                }
                            }
                            break;
                        case SEM_POSITION:
                            offXYZ = (int)input.offset;
                            sourceXYZ = sources[CheckURI(input.source)];
                            break;
                        case SEM_NORMAL:
                            offNORMAL = (int)input.offset;
                            sourceNORMAL = sources[CheckURI(input.source)];
                            conv.FVF |= D3DFVF.NORMAL;
                            break;
                        case SEM_COLOR:
                            offCOLOR = (int)input.offset;
                            sourceCOLOR = sources[CheckURI(input.source)];
                            conv.FVF |= D3DFVF.DIFFUSE;
                            break;
                        case SEM_TEXCOORD:
                            if (texCount == 2) throw new Exception("Too many texcoords!");
                            if(texCount == 1) {
                                offUV2 = (int)input.offset;
                                sourceUV2 = sources[CheckURI(input.source)];
                                conv.FVF &= ~D3DFVF.TEX1;
                                conv.FVF |= D3DFVF.TEX2;
                            } else {
                                offUV1 = (int)input.offset;
                                sourceUV1 = sources[CheckURI(input.source)];
                                if ((conv.FVF & D3DFVF.TEX2) != D3DFVF.TEX2) conv.FVF |= D3DFVF.TEX1;
                            }
                            texCount++;
                            break;
                    }
                }
                int vertexOffset = vertices.Count;
                for (int i = 0; i <  indexCount; i++) {
                    int idx = i * pStride;
                    var vert = new VertexPositionNormalDiffuseTextureTwo(
                        VecAxis(up, sourceXYZ.GetXYZ(pRefs[idx + offXYZ])),
                        offNORMAL == int.MinValue ? Vector3.Zero : VecAxis(up, sourceNORMAL.GetXYZ(pRefs[idx + offNORMAL])),
                        offCOLOR == int.MinValue ? (uint)Color4.White.ToRgba() : (uint)sourceCOLOR.GetColor(pRefs[idx + offCOLOR]).ToRgba(),
                        offUV1 == int.MinValue ? Vector2.Zero : sourceUV1.GetUV(pRefs[idx + offUV1]),
                        offUV2 == int.MinValue ? Vector2.Zero : sourceUV2.GetUV(pRefs[idx + offUV2])
                    );
                    var hash = HashVert(ref vert);
                    var vertIdx = FindDuplicate(hashes,vertices, vertexOffset, ref vert, hash);
                    if (indices.Count >= ushort.MaxValue)
                        throw new Exception("Too many indices");
                    if(vertIdx == -1) {
                        if (vertices.Count + 1 >= ushort.MaxValue)
                            throw new Exception("Too many vertices");
                        indices.Add((ushort)(vertices.Count - vertexOffset));
                        vertices.Add(vert);
                        hashes.Add(hash);
                    } else {
                        indices.Add((ushort)(vertIdx - vertexOffset));
                    }
                }
                drawcalls.Add(new ColladaDrawcall() { 
                    StartIndex = startIdx,
                    StartVertex = vertexOffset,
                    EndVertex = vertices.Count - 1,
                    TriCount = (indices.Count - startIdx) / 3,
                    Material = material
                });
            }
            conv.Indices = indices.ToArray();
            conv.Vertices = vertices.ToArray();
            conv.Drawcalls = drawcalls.ToArray();
            conv.CalculateDimensions();
            return conv;
        }

        public static int HashVert(ref VertexPositionNormalDiffuseTextureTwo vert)
        {
            unchecked {
                int hash = (int)2166136261;
                hash = hash * 16777619 ^ vert.Position.GetHashCode();
                hash = hash * 16777619 ^ vert.Normal.GetHashCode();
                hash = hash * 16777619 ^ vert.TextureCoordinate.GetHashCode();
                hash = hash * 16777619 ^ vert.TextureCoordinateTwo.GetHashCode();
                hash = hash * 16777619 ^ vert.Diffuse.GetHashCode();
                return hash;
            }
        }
        public static int FindDuplicate(List<int> hashes, List<VertexPositionNormalDiffuseTextureTwo> buf, int startIndex, ref VertexPositionNormalDiffuseTextureTwo search, int hash)
        {
            for (int i = startIndex; i < buf.Count; i++) {
                if (hashes[i] != hash) continue;
                if (buf[i].Position != search.Position) continue;
                if (buf[i].Normal != search.Normal) continue;
                if (buf[i].TextureCoordinate != search.TextureCoordinate) continue;
                if (buf[i].Diffuse != search.Diffuse) continue;
                if (buf[i].TextureCoordinateTwo != search.TextureCoordinateTwo) continue;
                return i;
            }
            return -1;
        }
        class GeometrySource
        {
            float[] array;
            int stride;
            int offset;
            public int Count { get; private set; }
            public GeometrySource(CL.source src, Dictionary<string, float[]> arrays)
            {
                var acc = src.technique_common.accessor;
                array = arrays[CheckURI(acc.source)];
                stride = (int)acc.stride;
                offset = (int)acc.offset;
                Count = (int)acc.count;
            }
            public Color4 GetColor(int index)
            {
                var i = offset + (index * stride);
                if (stride == 4)
                    return new Color4(
                        array[i],
                        array[i + 1],
                        array[i + 2],
                        array[i + 3]
                    );
                else if (stride == 3)
                    return new Color4(
                        array[i],
                        array[i + 1],
                        array[i + 2],
                        1
                    );
                else
                    throw new Exception("Color Unhandled stride " + stride);
            }
            public Vector3 GetXYZ(int index)
            {
                if (stride != 3) throw new Exception("Vec3 Unhandled stride " + stride);
                var i = offset + (index * stride);
                return new Vector3(
                    array[i],
                    array[i + 1],
                    array[i + 2]
                );
            }
            public Vector2 GetUV(int index)
            {
                if (stride != 2) throw new Exception("Vec2 Unhandled stride " + stride);
                var i = offset + (index * stride);
                return new Vector2(
                    array[i],
                    array[i + 1]
                );
            }
        }

        static Matrix4x4 GetMatrix(CL.UpAxisType ax, string text)
        {
            var floats = FloatArray(text);
            Matrix4x4 mat;
            if (floats.Length == 16)
                mat = new Matrix4x4(
                    floats[0], floats[4], floats[8], floats[12],
                    floats[1], floats[5], floats[9], floats[13],
                    floats[2], floats[6], floats[10], floats[14],
                    floats[3], floats[7], floats[11], floats[15]
                );
            else if (floats.Length == 9)
                mat = new Matrix4x4(
                    floats[0], floats[1], floats[2], 0,
                    floats[3], floats[4], floats[5], 0,
                    floats[6], floats[7], floats[8], 0,
                    0, 0, 0, 1
                );
            else
                throw new Exception("Invalid Matrix: " + floats.Length + " elements");
            if (ax == CL.UpAxisType.Z_UP)
            {
                var translation = mat.Translation;
                translation = new Vector3(translation.X, translation.Z, translation.Y) * new Vector3(1, 1, -1);

                var rotq = mat.ExtractRotation();
                var rot = Matrix4x4.CreateFromQuaternion(
                    new Quaternion(rotq.X,rotq.Z, -rotq.Y, rotq.W)
                );
                return rot * Matrix4x4.CreateTranslation(translation);
            }
            else if (ax == CL.UpAxisType.Y_UP)
                return mat;
            else
                throw new Exception("X_UP Unsupported");
        }
        static Vector3 VecAxis(CL.UpAxisType ax, Vector3 vec)
        {
            if (ax == CL.UpAxisType.Z_UP)
                return new Vector3(vec.X, vec.Z, vec.Y) * new Vector3(1, 1, -1);
            else if (ax == CL.UpAxisType.Y_UP)
                return vec;
            else
                throw new Exception("X_UP Unsupported");
        }
        static string CheckURI(string s)
        {
            if (s[0] != '#') throw new Exception("Don't support external dae refs");
            return s.Substring(1);
        }
        static string[] Tokens(string s) => s.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        static float[] FloatArray(string s) => Tokens(s).Select((x) => float.Parse(x, CultureInfo.InvariantCulture)).ToArray();
        static int[] IntArray(string s) => Tokens(s).Select((x) => int.Parse(x, CultureInfo.InvariantCulture)).ToArray();
        static float[] FloatArray(dynamic arr) => FloatArray(arr.Text);

        static bool TryParseColor(string s, out Color4 col)
        {
            col = Color4.White;
            try
            {
                var floats = FloatArray(s);
                if (floats.Length != 3 && floats.Length != 4) return false;
                col.R = floats[0];
                col.G = floats[1];
                col.B = floats[2];
                if (floats.Length > 3) col.A = floats[3];
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}