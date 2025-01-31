﻿Shader "Custom/River" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Specular("Specular", Color) = (0.2, 0.2, 0.2)
	}
	SubShader {
		Tags { "RenderType"="Transparent" "Queue"="Transparent+1" }
		LOD 200
		
		CGPROGRAM
		#pragma surface surf StandardSpecular alpha vertex:vert// fullforwardshadows
		#pragma multi_compile _ HEX_MAP_EDIT_MODE
		#pragma target 3.0

		#include "Water.cginc"
		#include "HexData.cginc"

		sampler2D _MainTex;

		struct Input {
			float2 uv_MainTex;
			float2 visibility;
		};

		half _Glossiness;
		half _Specular;
		fixed4 _Color;

		void vert(inout appdata_full v, out Input data) {
			UNITY_INITIALIZE_OUTPUT(Input, data);

			float4 hex0 = GetHexData(v, 0);
			float4 hex1 = GetHexData(v, 1);

			data.visibility.x = hex0.x * v.color.x + hex1.x * v.color.y;
			data.visibility.x = lerp(0.25, 1, data.visibility.x);
			data.visibility.y = hex0.y * v.color.x + hex1.y * v.color.y;
		}

		void surf (Input IN, inout SurfaceOutputStandardSpecular o) {
			float water = River(IN.uv_MainTex, _MainTex);

			float explored = IN.visibility.y;
			fixed4 c = saturate(_Color + water);
			o.Albedo = c.rgb * IN.visibility.x;
			o.Specular = _Specular * explored;
			o.Smoothness = _Glossiness;
			o.Occlusion = explored;
			o.Alpha = c.a * explored;
		}
		ENDCG
	}
	FallBack "Diffuse"
}