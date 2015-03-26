﻿using System;
using System.Collections;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using ProBuilder2.Common;

using UnityEngine;

/**
 * ProBuilder 2.3.1 does not contain a color property on its serializableObject class, so we need to provide a 
 * replacement that does.
 */
namespace ProBuilder2.SerializationTmp
{

	[System.Serializable()]
	public class pb_Color
	{
		public float r, g, b, a;

		public static implicit operator Color(pb_Color c) 
		{
			return new Color(c.r, c.g, c.b, c.a);
		}

		public static implicit operator pb_Color(Color c)
		{
			return new pb_Color(c);
		}

		public pb_Color()
		{
			this.r = 0f;
			this.g = 0f;
			this.b = 0f;
			this.a = 0f;
		}

		public pb_Color(Color c)
		{
			this.r = c.r;
			this.g = c.g;
			this.b = c.b;
			this.a = c.a;
		}

		public pb_Color(float r, float g, float b, float a)
		{
			this.r = r;
			this.g = g;
			this.b = b;
			this.a = a;
		}
	}

	#if UNITY_WP8
	public class pb_SerializableObject
	{
		// pb_Object
		public Vector3[] vertices;
		public Vector2[] uv;
		public pb_Face[] faces;
		public int[][] sharedIndices;
		public int[][] sharedIndicesUV;

		// transform
		public Vector3 		t_position;
		public Quaternion 	t_rotation;
		public Vector3 		t_scale;
	}
	#else
	[Serializable()]		
	public class pb_SerializableObject : ISerializable
	{
		// pb_Object
		public Vector3[] 	vertices;
		public Vector2[] 	uv;
		public Color[]		color;
		public pb_Face[] 	faces;
		public int[][] 		sharedIndices;
		public int[][] 		sharedIndicesUV;

		// transform
		public Vector3 		t_position;
		public Quaternion 	t_rotation;
		public Vector3 		t_scale;

		public pb_SerializableObject(pb_Object pb)
		{
			this.vertices = pb.vertices;
			this.uv = pb.uv;
			if(pb.msh != null && pb.msh.colors != null && pb.msh.colors.Length == pb.vertexCount)
			{
				this.color = pb.msh.colors;
			}
			else
			{
				this.color = new Color[pb.vertexCount];
				for(int i = 0; i < this.color.Length; i++)
					this.color[i] = Color.white;
			}
			this.faces = pb.faces;
			this.sharedIndices = (int[][])pb.GetSharedIndices().ToArray();
			this.sharedIndicesUV = (int[][])pb.GetSharedIndicesUV().ToArray();

			// Transform
			this.t_position = pb.transform.position;
			this.t_rotation = pb.transform.localRotation;
			this.t_scale = pb.transform.localScale;
		}

		public void Print()
		{
			Debug.Log(	"vertices: " + vertices.ToFormattedString(", ") +
						"\nuv: " + uv.ToFormattedString(", ") +
						"\nsharedIndices: " + ((pb_IntArray[])sharedIndices.ToPbIntArray()).ToFormattedString(", ") +
						"\nfaces: " + faces.ToFormattedString(", ")
						);
		}

		// OnSerialize
		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			// pb_object
			info.AddValue("vertices", 			System.Array.ConvertAll(vertices, x => (pb_Vector3)x),	typeof(pb_Vector3[]));
			info.AddValue("uv", 				System.Array.ConvertAll(uv, x => (pb_Vector2)x), 		typeof(pb_Vector2[]));
			info.AddValue("color", 				System.Array.ConvertAll(color, x => (pb_Color)x), 		typeof(pb_Color[]));
			info.AddValue("faces", 				faces, 													typeof(pb_Face[]));
			info.AddValue("sharedIndices", 		sharedIndices, 											typeof(int[][]));
			info.AddValue("sharedIndicesUV",	sharedIndicesUV, 										typeof(int[][]));

			// transform
			info.AddValue("t_position", 		(pb_Vector3)t_position,									typeof(pb_Vector3));
			info.AddValue("t_rotation", 		(pb_Vector4)t_rotation,									typeof(pb_Vector4));
			info.AddValue("t_scale", 			(pb_Vector3)t_scale, 									typeof(pb_Vector3));
		}

		// The pb_SerializableObject constructor is used to deserialize values. 
		public pb_SerializableObject(SerializationInfo info, StreamingContext context)
		{
			/// Vertices
			pb_Vector3[] pb_vertices = (pb_Vector3[]) info.GetValue("vertices", typeof(pb_Vector3[]));
			this.vertices = System.Array.ConvertAll(pb_vertices, x => (Vector3)x);
			
			/// UVs
			pb_Vector2[] pb_uv = (pb_Vector2[]) info.GetValue("uv", typeof(pb_Vector2[]));
			this.uv = System.Array.ConvertAll(pb_uv, x => (Vector2)x);
			
			/// Colors
			pb_Color[] pb_color = (pb_Color[]) info.GetValue("color", typeof(pb_Color[]));
			this.color = System.Array.ConvertAll(pb_color, x => (Color)x);

			/// Faces
			this.faces = (pb_Face[]) info.GetValue("faces", typeof(pb_Face[]));

			// Shared Indices
			this.sharedIndices = (int[][]) info.GetValue("sharedIndices", typeof(int[][]));

			// Shared Indices UV
			this.sharedIndicesUV = (int[][]) info.GetValue("sharedIndicesUV", typeof(int[][]));

			this.t_position = (pb_Vector3) info.GetValue("t_position", typeof(pb_Vector3));
			this.t_rotation = (pb_Vector4) info.GetValue("t_rotation", typeof(pb_Vector4));
			this.t_scale = (pb_Vector3) info.GetValue("t_scale", typeof(pb_Vector3));
		}
	}
	#endif
}