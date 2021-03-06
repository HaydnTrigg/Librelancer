﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace LibreLancer.Shaders
{
    using System;
    
    public class NebulaExtPuff
    {
        private static byte[] vertex_bytes = new byte[338] {
27, 157, 3, 0, 196, 127, 180, 115, 255, 203, 244, 142, 5, 15, 91, 42, 205, 100, 42, 7, 213, 212, 169, 156, 177, 128, 98, 156, 123, 192,
196, 100, 110, 27, 83, 115, 209, 248, 164, 15, 205, 182, 184, 172, 128, 165, 122, 189, 195, 216, 101, 109, 58, 214, 96, 124, 69, 191, 222, 238,
214, 135, 139, 78, 171, 92, 83, 17, 89, 13, 253, 1, 143, 142, 55, 232, 45, 15, 160, 183, 47, 185, 253, 44, 154, 128, 178, 138, 189, 65,
208, 56, 25, 202, 253, 56, 195, 229, 62, 35, 78, 250, 82, 63, 214, 234, 181, 245, 120, 118, 87, 18, 23, 74, 179, 130, 110, 136, 227, 237,
182, 125, 181, 87, 175, 192, 222, 149, 116, 105, 103, 174, 185, 174, 100, 136, 41, 183, 192, 152, 58, 39, 203, 131, 19, 17, 109, 93, 164, 49,
250, 35, 26, 163, 63, 1, 64, 38, 211, 123, 100, 198, 147, 92, 36, 15, 110, 42, 94, 124, 251, 97, 231, 117, 30, 37, 4, 82, 75, 227,
73, 118, 102, 33, 201, 114, 144, 135, 250, 50, 89, 199, 88, 251, 143, 245, 30, 2, 228, 129, 60, 174, 23, 101, 50, 55, 181, 201, 235, 253,
254, 250, 74, 182, 202, 7, 57, 62, 135, 64, 30, 226, 120, 139, 78, 200, 251, 244, 47, 198, 234, 69, 184, 80, 126, 23, 242, 128, 88, 74,
220, 112, 196, 199, 86, 155, 130, 64, 124, 0, 251, 245, 58, 175, 49, 185, 130, 240, 124, 9, 240, 183, 143, 187, 64, 126, 14, 20, 181, 19,
109, 207, 78, 28, 189, 21, 125, 98, 245, 236, 171, 135, 112, 191, 253, 92, 236, 47, 139, 179, 95, 176, 252, 92, 17, 33, 222, 125, 38, 140,
150, 73, 120, 35, 84, 90, 152, 221, 212, 15, 185, 232, 79, 14, 201, 242, 133, 210, 200, 207, 198, 183, 0, 230, 217, 94, 93, 114, 145, 226,
4, 220, 149, 180, 252, 141, 135, 69
};
        private static byte[] fragment_bytes = new byte[201] {
27, 68, 1, 32, 140, 195, 184, 225, 91, 148, 242, 162, 28, 45, 154, 136, 160, 38, 231, 39, 244, 219, 155, 228, 1, 69, 191, 182, 168, 163,
117, 229, 219, 146, 38, 158, 212, 196, 4, 117, 242, 5, 203, 29, 97, 64, 252, 183, 107, 191, 126, 79, 65, 144, 149, 6, 24, 20, 38, 24,
104, 6, 233, 54, 9, 242, 241, 214, 28, 58, 34, 234, 16, 114, 179, 248, 7, 63, 179, 130, 208, 139, 138, 178, 203, 114, 189, 12, 249, 246,
232, 65, 224, 101, 196, 77, 88, 111, 162, 11, 143, 254, 195, 43, 75, 211, 171, 111, 101, 240, 207, 116, 34, 109, 147, 31, 62, 96, 160, 95,
11, 146, 231, 35, 186, 139, 240, 124, 153, 68, 142, 249, 56, 250, 27, 133, 205, 29, 206, 231, 67, 204, 152, 162, 220, 70, 12, 221, 4, 96,
111, 184, 49, 8, 244, 7, 11, 35, 187, 10, 154, 88, 246, 231, 173, 199, 75, 125, 211, 122, 190, 8, 32, 147, 57, 22, 220, 216, 79, 3,
184, 246, 249, 131, 251, 78, 186, 4, 170, 178, 72, 4, 88, 75, 17, 37, 14, 39, 60, 35, 0
};
        static ShaderVariables[] variants;
        private static bool iscompiled = false;
        private static int GetIndex(ShaderFeatures features)
        {
            ShaderFeatures masked = (features & ((ShaderFeatures)(0)));
            return 0;
        }
        public static ShaderVariables Get(ShaderFeatures features)
        {
            return variants[GetIndex(features)];
        }
        public static ShaderVariables Get()
        {
            return variants[0];
        }
        public static void Compile()
        {
            if (iscompiled)
            {
                return;
            }
            iscompiled = true;
            ShaderVariables.Log("Compiling NebulaExtPuff");
            string vertsrc;
            string fragsrc;
            vertsrc = ShCompHelper.FromArray(vertex_bytes);
            fragsrc = ShCompHelper.FromArray(fragment_bytes);
            variants = new ShaderVariables[1];
            variants[0] = ShaderVariables.Compile(vertsrc, fragsrc, "");
        }
    }
}
