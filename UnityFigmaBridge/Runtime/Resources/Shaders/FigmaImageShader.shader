// Based on Unity's Default UI Shader
// And 2D Sdf functions from Inigo Quilez
// https://iquilezles.org/articles/distfunctions2d/

Shader "Figma/FigmaImageShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        
        // Figma specific properties below
        
        // Corner radius defines corner for rectangle shapes, one per corner
        _CornerRadius ("Corner Radius", Vector) = (0, 0, 0, 0)
        
        _StrokeWidth ("Stroke Width", Float) = 2
        _StrokeColor ("Stroke Color", Color) = (1,1,1,1)
        _FillColor ("Fill Color", Color) = (1,1,1,1)
        
        // End Figma properties
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #pragma multi_compile_local _ LINEAR_GRADIENT RADIAL_GRADIENT
            #pragma multi_compile_local _ STROKE
            #pragma multi_compile_local _ SHAPE_RECTANGLE SHAPE_ELLIPSE SHAPE_STAR
            #pragma multi_compile_local _ ARC_ANGLE_RANGE
            #pragma multi_compile_local _ CLAMP_TEXTURE

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                // Normalised position within rect transform (0,1 in x,y) and rectransform size
                float4 normalised_pos_size : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 relative_rt_position : TEXCOORD2;
                float2 normalised_position : TEXCOORD3;
                float4 clamped_corner_radius : TEXCOORD4;
                
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Define GLSL style Mod function   
            #define mod(x, y) (x - y * floor(x / y))    

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _FillColor;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            // Figma specific properties below
            float4 _CornerRadius;
            float4 _StrokeColor;
            float _StrokeWidth;

            float4 _GradientColors[16];
            float _GradientStops[16];
            float _GradientNumStops;
            // Min/Max angle range and inner radius (if enabled)
            float4 _ArcAngleRangeInnerRadius;
            // Handle positions for gradient (points)
            float _GradientHandlePositions[6];
            
            // End Figma properties

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

                OUT.color = v.color;

               
                #if SHAPE_RECTANGLE
                     // Clamp corner radius (TR/BR/TL/BL) - the combined radius on any edge should not exceed the length
                    float cornerSizeTopRatio=max(1,(_CornerRadius.x+_CornerRadius.z)/v.normalised_pos_size.z);
                    float cornerSizeBottomRatio=max(1,(_CornerRadius.y+_CornerRadius.w)/v.normalised_pos_size.z);
                    float cornerSizeLeftRatio=max(1,(_CornerRadius.z+_CornerRadius.w)/v.normalised_pos_size.w);
                    float cornerSizeRightRatio=max(1,(_CornerRadius.x+_CornerRadius.y)/v.normalised_pos_size.w);

                    float4 clampedCornerRadius=float4(_CornerRadius.x,_CornerRadius.y,_CornerRadius.z,_CornerRadius.w);
                
                    // Divide by the largest of the relevant ratios such that the corner is only constrained by the smallest side.
                    clampedCornerRadius.x/=max(cornerSizeTopRatio,cornerSizeRightRatio);
                    clampedCornerRadius.y/=max(cornerSizeBottomRatio,cornerSizeRightRatio);
                    clampedCornerRadius.z/=max(cornerSizeTopRatio,cornerSizeLeftRatio);
                    clampedCornerRadius.w/=max(cornerSizeBottomRatio,cornerSizeLeftRatio);
                
                    OUT.clamped_corner_radius=clampedCornerRadius;
                #endif
                

                // Encode the relative position and size pos
                OUT.normalised_position= float2(v.normalised_pos_size.x,v.normalised_pos_size.y);
                OUT.relative_rt_position=float4(v.normalised_pos_size.x*v.normalised_pos_size.z,v.normalised_pos_size.y*v.normalised_pos_size.w,v.normalised_pos_size.z,v.normalised_pos_size.w);
                
                return OUT;
            }

            

            // From IQ - https://www.shadertoy.com/view/4llXD7
            float distance_from_rounded_rect_border(float4 relative_rt_position,float4 corner_radius)
            {
                // Width/Height
                float2 b= float2(relative_rt_position.z*0.5f, relative_rt_position.w*0.5f);
                // Position
                float2 p=relative_rt_position.xy-b;
                // Corner radius (TR/BR/TL/BL)
                float4 r=corner_radius;
                r.xy = (p.x>0.0)?r.xy : r.zw;
                r.x  = (p.y>0.0)?r.x  : r.y;
                float2 q = abs(p)-b+r.x;
                return min(max(q.x,q.y),0.0) + length(max(q,0.0)) - r.x;
            }

            float distance_from_ellipse_fast_inaccurate(float2 normalised_position,float4 relative_rt_position)
            {
                 // This fast and works but irregular width of ellipse
                half2 rectSize=float2(relative_rt_position.z,relative_rt_position.w);
                float2 relativeDistanceFromCenter=2*(normalised_position-float2(0.5,0.5f));
                float normalised_distance_from_shape=-(1-length(relativeDistanceFromCenter));
                return normalised_distance_from_shape*length(rectSize*0.5f);
            }

            
            //From https://iquilezles.org/articles/distfunctions2d/
            float distance_from_five_star(float4 relative_rt_position)
            {
                float2 ab= float2(relative_rt_position.z*0.5f, relative_rt_position.w*0.5f);
                float2 p=relative_rt_position.xy-ab;
                // Right now length will be smallest of size. TODO - Implement non uniform scaling
                float r=min(ab.x,ab.y);
                float rf=0.38;
        
                const float2 k1 = float2(0.809016994375, -0.587785252292);
                const float2 k2 = float2(-k1.x,k1.y);
                p.x = abs(p.x);
                p -= 2.0*max(dot(k1,p),0.0)*k1;
                p -= 2.0*max(dot(k2,p),0.0)*k2;
                p.x = abs(p.x);
                p.y -= r;
                float2 ba = rf*float2(-k1.y,k1.x) - float2(0,1);
                float h = clamp( dot(p,ba)/dot(ba,ba), 0.0, r );
                return length(p-ba*h) * sign(p.y*ba.x-p.x*ba.y);
            }

        
            // From IQ - https://www.shadertoy.com/view/4sS3zz
            float distance_from_ellipse(float4 relative_rt_position)
            {
                float2 ab= float2(relative_rt_position.z*0.5f, relative_rt_position.w*0.5f);
                float2 p=relative_rt_position.xy-ab;
                
                p = abs(p); if( p.x > p.y ) {p=p.yx;ab=ab.yx;}

                float l = ab.y*ab.y - ab.x*ab.x;
                float m = ab.x*p.x/l;      float m2 = m*m; 
                float n = ab.y*p.y/l;      float n2 = n*n; 
                float c = (m2+n2-1.0)/3.0; float c3 = c*c*c;
                float q = c3 + m2*n2*2.0;
                float d = c3 + m2*n2;
                float g = m + m*n2;
                float co;
                if( d<0.0 )
                {
                    float h = acos(q/c3)/3.0;
                    float s = cos(h);
                    float t = sin(h)*sqrt(3.0);
                    float rx = sqrt( -c*(s + t + 2.0) + m2 );
                    float ry = sqrt( -c*(s - t + 2.0) + m2 );
                    co = (ry+sign(l)*rx+abs(g)/(rx*ry)- m)/2.0;
                }
                else
                {
                    float h = 2.0*m*n*sqrt( d );
                    float s = sign(q+h)*pow(abs(q+h), 1.0/3.0);
                    float u = sign(q-h)*pow(abs(q-h), 1.0/3.0);
                    float rx = -s - u - c*4.0 + 2.0*m2;
                    float ry = (s - u)*sqrt(3.0);
                    float rm = sqrt( rx*rx + ry*ry );
                    co = (ry/sqrt(rm-rx)+2.0*g/rm-m)/2.0;
                }
                float2 r = ab * float2(co, sqrt(1.0-co*co));
                return length(r-p) * sign(p.y-r.y);
            }

            float4 GammaToLinearIfNeeded(float4 color)
            {
            #if UNITY_COLORSPACE_GAMMA
                return color;
            #else
                return float4(GammaToLinearSpace(color.rgb), color.a);
            #endif
            }
        
            float4 GetGradientColor(float percAlongGradient)
            {
                // Apply gradient
                // From https://github.com/rokotyan/Linear-Gradient-Shader/blob/master/resources/LinearGradient_frag.glsl
                float4 gradientColor = lerp(_GradientColors[0], _GradientColors[1], smoothstep( _GradientStops[0],  _GradientStops[1], percAlongGradient ) );
                for ( int i=1; i<_GradientNumStops-1; ++i ) {
                    gradientColor = lerp(gradientColor, _GradientColors[i+1], smoothstep( _GradientStops[i],  _GradientStops[i+1], percAlongGradient ) );
                }

                // If the setting of the color space is set to Linear,
                // convert the color to linear space after the interpolation.
                return  GammaToLinearIfNeeded(gradientColor);
            }


            fixed4 frag(v2f IN) : SV_Target
            {

                // By default use fill*vertex colour as passed through
                half4 shapeColor=_FillColor;

                 const float half_pi = 1.57079632679;
                
                // If gradient, replace fill color
                #if LINEAR_GRADIENT
                    // TODO - Optimise
                    // Get distance vector for handle positions (used for both angle and length)
                    // Unity sets top as 1, bottom as 0 (inverse of Figma) so need to reverse
                    float2 gradient_p0=float2(_GradientHandlePositions[0],1.0f-_GradientHandlePositions[1]);
                    float2 gradient_p1=float2(_GradientHandlePositions[2],1.0f-_GradientHandlePositions[3]);
                    float2 gradient_p2=float2(_GradientHandlePositions[4],1.0f-_GradientHandlePositions[5]);
                    float2 gradientDistanceDirection=gradient_p1-gradient_p0;
                    float2 gradientDistanceNormal=gradient_p2-gradient_p0;
                
                    // Calc gradient angle for inverse  (transform UV pos into gradient space)
                    float gradientAngle=half_pi-atan2(-gradientDistanceNormal.y,gradientDistanceNormal.x);
                    float c=cos(gradientAngle);
                    float s=sin(gradientAngle);
                    // Build rotation matrix
                    float2x2 rotationMatrix = float2x2( c, -s, s, c);
                    // Offset point to relative to center
                    float2 relativeToCenterPos=IN.normalised_position-gradient_p0;
                    // Transform point to find pos in gradient space
                    float2 rotatedNormalisedPosition = mul ( relativeToCenterPos, rotationMatrix);
                    // Divide by gradientDistanceLength to adjust scale of gradient length
                    // Shift to range 0..1
                    float gradientPosition=saturate(rotatedNormalisedPosition.x/length(gradientDistanceDirection));
                
                    float4 gradientColor=GetGradientColor(gradientPosition);
                    shapeColor.rgb*=gradientColor.rgb;
                    shapeColor.a*=gradientColor.a;
                #endif
                
                #if RADIAL_GRADIENT

                   
                    // Unity sets top as 1, bottom as 0 (inverse of Figma) so need to reverse Y
                    float2 gradient_p0=float2(_GradientHandlePositions[0],1.0f-_GradientHandlePositions[1]);
                    float2 gradient_p1=float2(_GradientHandlePositions[2],1.0f-_GradientHandlePositions[3]);
                    float2 gradient_p2=float2(_GradientHandlePositions[4],1.0f-_GradientHandlePositions[5]);

                    float2 relativeToCenterPos=IN.normalised_position-gradient_p0;
                    
                    // Calculate distance vectors in both directions. These will not necessarily be perpendicular in
                    // normalised space
                    float2 gradientDistanceVec1=gradient_p1-gradient_p0;
                    float2 gradientDistanceVec2=gradient_p2-gradient_p0;

                    // Calculate distance along gradient direction from gradient center in both directions
                    float gradientAngle1=half_pi-atan2(-gradientDistanceVec1.y,gradientDistanceVec1.x);
                    float c1=cos(gradientAngle1);
                    float s1=sin(gradientAngle1);
                    float2x2 rotationMatrix1 = float2x2( c1, -s1, s1, c1);
                    float2 rotatedNormalisedPosition1 = mul ( relativeToCenterPos, rotationMatrix1);
                    float distanceToGradientPos2=rotatedNormalisedPosition1.x/length(gradientDistanceVec2);

                    float gradientAngle2=half_pi-atan2(-gradientDistanceVec2.y,gradientDistanceVec2.x);
                    float c2=cos(gradientAngle2);
                    float s2=sin(gradientAngle2);
                    float2x2 rotationMatrix2 = float2x2( c2, -s2, s2, c2);
                    float2 rotatedNormalisedPosition2 = mul ( relativeToCenterPos, rotationMatrix2);
                    float distanceToGradientPos1=rotatedNormalisedPosition2.x/length(gradientDistanceVec1);

                    // Calculate overall distance based upon these two values
                    float gradientPosition=saturate(length(float2(abs(distanceToGradientPos1),abs(distanceToGradientPos2))));
                    float4 gradientColor=GetGradientColor(gradientPosition);
                    shapeColor.rgb*=gradientColor.rgb;
                    shapeColor.a*=gradientColor.a;
                
                #endif
                
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd)*shapeColor;
                #if CLAMP_TEXTURE
                    // Calculate within bounds factor relative to 0..1 (without branching to keep performant)
                    float withinBoundsFactor= 1.0f-step(IN.texcoord.x,0.0f) - step(1.0f, IN.texcoord.x)-step(IN.texcoord.y,0.0f)- step(1.0f, IN.texcoord.y);
                    // Lerp to transparent if outside this range
                    color=lerp(half4(0,0,0,0),color,withinBoundsFactor);
                #endif
                
                #if SHAPE_ELLIPSE
                   #if STROKE
                        // We need an accurate ellipse SDF function if we are using stroke
                        float distance_from_shape=distance_from_ellipse(IN.relative_rt_position);
                    #else
                        // Otherwise simple and fast
                        float distance_from_shape=distance_from_ellipse_fast_inaccurate(IN.normalised_position,IN.relative_rt_position);
                    #endif
                #else
                    #if SHAPE_STAR
                        float distance_from_shape=distance_from_five_star(IN.relative_rt_position);
                    #else
                        // Calculate SDF value for this fragment (distance from edge of shape)
                        float distance_from_shape=distance_from_rounded_rect_border(IN.relative_rt_position,IN.clamped_corner_radius);
                    #endif
                #endif
                
                const float scale=1;
                // Calculate alpha value based on distance from edge
                
                float alpha = saturate(-distance_from_shape* scale);
                
                #if ARC_ANGLE_RANGE
                    // Limit depending on angle range
                    float2 normalised_pos_center=IN.normalised_position-float2(0.5f,0.5f);
                    float angle=atan2(normalised_pos_center.y,normalised_pos_center.x);
                    float within_range=angle>_ArcAngleRangeInnerRadius.x && angle<(_ArcAngleRangeInnerRadius.x+_ArcAngleRangeInnerRadius.y);
                    alpha*=within_range;
                #endif
                
                
                #if STROKE
                    // Apply stroke color (rewrite?)
                    float strokeBlend = saturate(-(distance_from_shape + _StrokeWidth)*scale);
                    color = half4(lerp(_StrokeColor.rgb, color.rgb, strokeBlend), lerp(_StrokeColor.a, color.a, strokeBlend));
                #endif
                color.a *= alpha;

                // Finally multiply by base colour (to make shadow component, alpha groups work
                color*=IN.color;
                
                
                #ifdef UNITY_UI_CLIP_RECT
                    color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                    clip (color.a - 0.001);
                #endif

                // For accurate blending that matches the Figma doc, we want to convert the alpha value back
                // to gamma space prior to blend
                #if UNITY_COLORSPACE_GAMMA
                    return color;
                #else
                    return float4(color.rgb, LinearToGammaSpace(color.a).r);
                #endif
                 
                return color;
                
            }

        
        
        ENDCG
        }
    }
}