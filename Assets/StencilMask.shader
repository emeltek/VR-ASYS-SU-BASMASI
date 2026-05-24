Shader "Custom/StencilMask"
{
    SubShader
    {
        // Objeyi çimenlerden önce çizmek için Geometry-1 sırasına alıyoruz
        Tags { "RenderType"="Opaque" "Queue"="Geometry-1" }
        
        // Ekrana renk veya derinlik çizme (Görünmez olmasını sağlar)
        ColorMask 0
        ZWrite Off

        Stencil
        {
            Ref 1
            Comp Always
            Pass Replace
        }

        Pass
        {
            // Boş pass, sadece stencil tetiklemek için
        }
    }
}