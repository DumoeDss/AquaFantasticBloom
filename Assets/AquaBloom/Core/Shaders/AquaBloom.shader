Shader "Custom/RenderFeature/AquaBloom"
{
	HLSLINCLUDE
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
	uniform float4 _MainTex_ST;
	float4 _MainTex_TexelSize;

	float4 _Filtering_Params; // x: scatter, y: clamp, z: threshold (linear), w: threshold knee
	#define Scatter             _Filtering_Params.x
	#define ClampMax            _Filtering_Params.y
	#define Threshold           _Filtering_Params.z
	#define ThresholdKnee       _Filtering_Params.w

	half4 _Bloom_Params;
	#define _Tint             _Bloom_Params.yzw
	#define _Intensity             _Bloom_Params.x

	uniform half _BlurOffset;

	half4 _FantasticTint;
	half _FullScreenBloomIntensity;
	half _FantasticIntensity;

	TEXTURE2D(_MainTex);
	TEXTURE2D(_OriginTex);
	TEXTURE2D(_GlowTex);
	TEXTURE2D(_SourceTexLowMip);
	TEXTURE2D(_LensDirt_Texture);

	SAMPLER(sampler_MainTex);
	
	half _LensDirt;
	half _LensDirtIntensity;
	float4 _LensDirt_Params;
	#define LensDirtScale           _LensDirt_Params.xy
	#define LensDirtOffset          _LensDirt_Params.zw

	struct v2f_DownSample
	{
		float4 vertex: SV_POSITION;
		float2 uv: TEXCOORD0;
		float4 uv01: TEXCOORD2;
		float4 uv23: TEXCOORD3;
	};
	
	struct v2f_UpSample
	{
		float4 vertex: SV_POSITION;
		float2 uv: TEXCOORD0;
		float4 uv01: TEXCOORD1;
		float4 uv23: TEXCOORD2;
		float4 uv45: TEXCOORD3;
		float4 uv67: TEXCOORD4;
	};

	struct v2f_Bloom
	{
		float4 vertex: SV_POSITION;
		float2 uv: TEXCOORD0;
	};

	struct appdata
	{
		float4 vertex : POSITION;
		float2 uv : TEXCOORD0;
	};

	v2f_Bloom Vert_Bloom(appdata v) {
		v2f_Bloom o;
		o.vertex = TransformObjectToHClip(v.vertex);
		o.uv = v.uv;
		return o;
	}

	half4 Frag_PreSample(v2f_Bloom i) : SV_Target
	{
		half3 color = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, i.uv).xyz;

		half brightness = Max3(color.r, color.g, color.b);
		half softness = clamp(brightness - Threshold + ThresholdKnee, 0.0, 2.0 * ThresholdKnee);
		softness = (softness * softness) / (4.0 * ThresholdKnee + 1e-4);
		half multiplier = max(brightness - Threshold, softness) / max(brightness, 1e-4);
		color *= multiplier;

		return half4(color, 1);
	}

	v2f_DownSample Vert_DownSample(appdata v)
	{
		v2f_DownSample o;
		o.vertex = TransformObjectToHClip(v.vertex);

		float2 uv = o.uv = v.uv;
		float2 _offset = float2(1 + _BlurOffset, 1 + _BlurOffset);
		o.uv01.xy = uv - _MainTex_TexelSize * _offset;
		o.uv01.zw = uv + _MainTex_TexelSize * _offset;
		o.uv23.xy = uv - float2(_MainTex_TexelSize.x, -_MainTex_TexelSize.y) * _offset;
		o.uv23.zw = uv + float2(_MainTex_TexelSize.x, -_MainTex_TexelSize.y) * _offset;


		return o;
	}

	half4 Frag_DownSample(v2f_DownSample i) : SV_Target
	{
		half4 sum = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * 4;
		sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv01.xy);
		sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv01.zw);
		sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv23.xy);
		sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv23.zw);
		sum *= 0.125;
		//half4 sum = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

		return sum;
	}
		
	v2f_UpSample Vert_UpSample(appdata v)
	{
		v2f_UpSample o;
		o.vertex = TransformObjectToHClip(v.vertex);
		float2 uv = o.uv = v.uv;
		_MainTex_TexelSize *= 0.5;
		float2 _offset = float2(1 + _BlurOffset, 1 + _BlurOffset);
		
		o.uv01.xy = uv + float2(-_MainTex_TexelSize.x * 2, 0) * _offset;
		o.uv01.zw = uv + float2(-_MainTex_TexelSize.x, _MainTex_TexelSize.y) * _offset;
		o.uv23.xy = uv + float2(0, _MainTex_TexelSize.y  *2) * _offset;
		o.uv23.zw = uv + _MainTex_TexelSize * _offset;
		o.uv45.xy = uv + float2(_MainTex_TexelSize.x  *2, 0) * _offset;
		o.uv45.zw = uv + float2(_MainTex_TexelSize.x, -_MainTex_TexelSize.y) * _offset;
		o.uv67.xy = uv + float2(0, -_MainTex_TexelSize.y *2) * _offset;
		o.uv67.zw = uv - _MainTex_TexelSize * _offset;
		
		return o;
	}
	
	half4 Frag_BlurUpSample(v2f_UpSample i): SV_Target
	{
		half4 sum = 0;
		sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv01.xy);
		sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv01.zw) * 2;
		sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv23.xy);
		sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv23.zw) * 2;
		sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv45.xy);
		sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv45.zw) * 2;
		sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv67.xy);
		sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv67.zw) * 2;
		sum *= 0.0833;
		return sum;
	}

	half4 Frag_GlowUpSample(v2f_UpSample i): SV_Target
	{
		half4 sum = 0;
		sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv01.xy);
		sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv01.zw) * 2;
		sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv23.xy);
		sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv23.zw) * 2;
		sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv45.xy);
		sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv45.zw) * 2;
		sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv67.xy);
		sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv67.zw) * 2;
		sum *= 0.0833;
		//half4 sum = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
		
		half4 lowMip = SAMPLE_TEXTURE2D(_SourceTexLowMip, sampler_MainTex, i.uv);

		return lerp(sum, lowMip, Scatter);
	}

	half4 Frag_Bloom(v2f_Bloom i) : SV_Target{
		half4 col = SAMPLE_TEXTURE2D(_OriginTex, sampler_MainTex, i.uv);

		half4 glow = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Intensity * half4(_Tint,1.0);
		half4 dirt = lerp(0, SAMPLE_TEXTURE2D(_LensDirt_Texture, sampler_MainTex, i.uv * LensDirtScale + LensDirtOffset) * _LensDirtIntensity,_LensDirt);
		col += dirt * glow + glow;
		return col;
	}	

	half4 blendLinearBurnDodge(half4 base, half4 blend, half burnOpacity, half dodgeOpacity) {
		return  clamp(base + blend - 1.0, 0, 1) * burnOpacity +
			clamp(base + blend, 0, 1) * dodgeOpacity +
			base * clamp((1 - burnOpacity - dodgeOpacity),0,1);
	}

	float3 ACESToneMapping(float3 color, float adapted_lum)
	{
		half A = 2.51f;
		half B = 0.03f;
		half C = 2.43f;
		half D = 0.59f;
		half E = 0.14f;

		color *= adapted_lum;

		float3 col = (color * (A * color + B)) / (color * (C * color + D) + E);

		col = pow(col, 0.91);
		return col;
	}

	half4 Frag_FantasticBloom(v2f_Bloom i) : SV_Target{
		half4 col = SAMPLE_TEXTURE2D(_OriginTex, sampler_MainTex, i.uv);
		half4 blur = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _FantasticTint;
		half4 glow = SAMPLE_TEXTURE2D(_GlowTex, sampler_MainTex, i.uv) * _Intensity * half4(_Tint,1.0);
		half4 dirt =  lerp(0,SAMPLE_TEXTURE2D(_LensDirt_Texture, sampler_MainTex, i.uv * LensDirtScale + LensDirtOffset) * _LensDirtIntensity,_LensDirt);

		col = blendLinearBurnDodge(col, blur, _FantasticIntensity, _FullScreenBloomIntensity) +dirt* glow+ glow;
		//col.rgb = ACESToneMapping(col.rgb, 0.6);
		return col;
	}

	ENDHLSL
	
	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass //0
		{
			Name "Bloom Prefilter"
			HLSLPROGRAM

			#pragma vertex Vert_Bloom
			#pragma fragment Frag_PreSample

			ENDHLSL
		}

		Pass //1
		{
			Name "Bloom DownSample"

			HLSLPROGRAM
			
			#pragma vertex Vert_DownSample
			#pragma fragment Frag_DownSample
			
			ENDHLSL
		}
		
		Pass //2
		{
			Name "Bloom UpSample Blur"

			HLSLPROGRAM
			
			#pragma vertex Vert_UpSample
			#pragma fragment Frag_BlurUpSample
			
			ENDHLSL
		}

		Pass //3
		{
			Name "Bloom UpSample Glow"

			HLSLPROGRAM
			
			#pragma vertex Vert_UpSample
			#pragma fragment Frag_GlowUpSample
			
			ENDHLSL
		}

		Pass //4
		{
			Name "Bloom Final"
			HLSLPROGRAM

			#pragma vertex Vert_Bloom
			#pragma fragment Frag_Bloom

			ENDHLSL
		}

		Pass //5
		{
			Name "Bloom Fantastic"
			HLSLPROGRAM
			#pragma vertex Vert_Bloom
			#pragma fragment Frag_FantasticBloom

			ENDHLSL
		}
	}
}


