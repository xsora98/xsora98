using System;
using System.Collections.Generic;
using System.Drawing;
using CSG.Sharp;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using RADataLayer;
using RAGraphicsControlLayer;
using ShapeLibrary;

namespace GraphicsControlLayer
{
    public class RenderControl
    {
        //This line is plug&play. Copy and paste it without modifications in every class file that needs logging
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static int _vertexArrayObject;
        public static int _vertexBufferObject;
        public static int vertex_buffer_object, color_buffer_object;

        public static float rotatingX=0.5f;
        public static float rotatingY=0.5f;
        public static float rotatingZ=0.5f;
    /*    public static List<Plane> test = new List<Plane>();
        public static List<Plane> testx = new List<Plane>();*/
        //lists for arrays
        public static List<float> verticesList = new List<float>();
        public static List<System.Numerics.Vector3> contourList = new List<System.Numerics.Vector3>();
        public static List<float> verticesColor = new List<float>();
        public static List<System.Numerics.Vector3> verticesListTest = new List<System.Numerics.Vector3>();

        public static bool isLoaded = false;

        //shader
        public static Shader shader;
        public static Shader lightingShader;
        private static string vertexShaderCode = @"#version 330 core
layout (location = 0) in vec3 aPos;
layout(location = 1) in vec3 vertexColor;
layout (location = 2) in vec3 aNormal;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;
out vec3 fragmentColor;
out vec3 Normal;
out vec3 FragPos;
void main(){
    gl_Position = vec4(aPos, 1.0) * model * view * projection; 
	fragmentColor=vertexColor;
    FragPos = vec3(vec4(aPos, 1.0) * model);
    Normal = aNormal * mat3(transpose(inverse(model)));
}";

        private static string fragmentShaderCode = @"#version 330

in vec3 fragmentColor;

out vec3 color;

void main(){
    color = fragmentColor;
}";

        private static string lightingShaderCode = @"#version 330

out vec3 color;

uniform vec3 objectColor; //The color of the object.
uniform vec3 lightColor; //The color of the light.
uniform vec3 lightPos; //The position of the light.
uniform vec3 viewPos; //The position of the view and/or of the player.

in vec3 Normal; //The normal of the fragment is calculated in the vertex shader.
in vec3 FragPos; //The fragment position.

void main(){
    //The ambient color is the color where the light does not directly hit the object.
    //You can think of it as an underlying tone throughout the object. Or the light coming from the scene/the sky (not the sun).
    float ambientStrength = 0.1;
    vec3 ambient = ambientStrength * lightColor;

    //We calculate the light direction, and make sure the normal is normalized.
    vec3 norm = normalize(Normal);
    vec3 lightDir = normalize(lightPos - FragPos); //Note: The light is pointing from the light to the fragment

    //The diffuse part of the phong model.
    //This is the part of the light that gives the most, it is the color of the object where it is hit by light.
    float diff = max(dot(norm, lightDir), 0.0); //We make sure the value is non negative with the max function.
    vec3 diffuse = diff * lightColor;


    //The specular light is the light that shines from the object, like light hitting metal.
    //The calculations are explained much more detailed in the web version of the tutorials.
    float specularStrength = 0.5;
    vec3 viewDir = normalize(viewPos - FragPos);
    vec3 reflectDir = reflect(-lightDir, norm);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), 32); //The 32 is the shininess of the material.
    vec3 specular = specularStrength * spec * lightColor;

    //At last we add all the light components together and multiply with the color of the object. Then we set the color
    //and makes sure the alpha value is 1
    vec3 result = (ambient + diffuse + specular) * objectColor;
    color = vec3(result);
    
    //Note we still use the light color * object color from the last tutorial.
    //This time the light values are in the phong model (ambient, diffuse and specular)
}";

        public static int ColorToRgba32(Color c)
        {
            return (int)((c.A << 24) | (c.B << 16) | (c.G << 8) | c.R);
        }

        public static void RenderFrame(Eto.OpenTK.WinForms.WinGLUserControl RenderPanel, Matrix4 lookAt, int _vertexArrayObject, List<float> test, float _angle)
        {
            RenderPanel.MakeCurrent();
            GL.ClearColor(Color.Purple);
            GL.Enable(EnableCap.DepthTest);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadMatrix(ref lookAt);
            GL.Rotate(_angle, 0.0f, 1.0f, 0.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.BindVertexArray(_vertexArrayObject);
            GL.DrawArrays(PrimitiveType.Triangles, 0, test.ToArray().Length);
            RenderPanel.SwapBuffers();
        }

        public static void RenderFrame(Eto.OpenTK.WinForms.WinGLUserControl RenderPanel, Matrix4 lookat, int _vertexArrayObject, List<float> test)
        {
            RenderPanel.MakeCurrent();

            GL.Enable(EnableCap.DepthTest);


            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadMatrix(ref lookat);

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.BindVertexArray(_vertexArrayObject);
            GL.DrawArrays(PrimitiveType.Triangles, 0, test.ToArray().Length);
            RenderPanel.SwapBuffers();
        }
        public static void RenderFrameWithContour(Eto.OpenTK.WinForms.WinGLUserControl RenderPanel, Matrix4 lookat, Matrix4 projection,Vector3 position)
        {

            RenderPanel.MakeCurrent();
            GL.ClearColor(Color.LightBlue);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.Enable(EnableCap.LineSmooth);
            GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);

            GL.Enable(EnableCap.PointSmooth);
            GL.Hint(HintTarget.PointSmoothHint, HintMode.Nicest);

            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadMatrix(ref lookat);

            GL.Enable(EnableCap.DepthTest);

            if (isLoaded)
            {
                lightingShader.UseProgram();
                int modelLoc = GL.GetUniformLocation(lightingShader.ProgramID, "model");
                int viewLoc = GL.GetUniformLocation(lightingShader.ProgramID, "view");
                int projLoc = GL.GetUniformLocation(lightingShader.ProgramID, "projection");
                var model = Matrix4.Identity;
                model *= Matrix4.CreateScale(0.01f);
                int objectColorLoc = GL.GetUniformLocation(lightingShader.ProgramID, "objectColor");
                int lightColorLoc = GL.GetUniformLocation(lightingShader.ProgramID, "lightColor");
                int lightPosLoc = GL.GetUniformLocation(lightingShader.ProgramID, "lightPos");
                int viewPosLoc = GL.GetUniformLocation(lightingShader.ProgramID, "viewPos");
                lightingShader.SetMatrix4(modelLoc, model);
                lightingShader.SetMatrix4(viewLoc, lookat);
                lightingShader.SetMatrix4(projLoc, projection);
                lightingShader.SetVector3(objectColorLoc, new Vector3(1.0f, 0.5f, 0.31f));
                lightingShader.SetVector3(lightColorLoc, new Vector3(1.0f, 1.0f, 1.0f));
                lightingShader.SetVector3(lightPosLoc, new Vector3(500f, 500f, 500f));
                lightingShader.SetVector3(viewPosLoc, new Vector3(500f, 500f, 500f));
                DrawOneSolid();
                shader.UseProgram();
                modelLoc = GL.GetUniformLocation(shader.ProgramID, "model");
                viewLoc = GL.GetUniformLocation(shader.ProgramID, "view");
                projLoc = GL.GetUniformLocation(shader.ProgramID, "projection");
                shader.SetMatrix4(modelLoc, model);
                shader.SetMatrix4(viewLoc, lookat);
                shader.SetMatrix4(projLoc, projection);
                DrawOneSolid();


            }
            GL.UseProgram(0);
            DrawMainCoordinateSystem();
            RenderPanel.SwapBuffers();
        }

        private static void DrawProductParts()
        {
            if (ProductContainer.listImportedProduct == null) return;
            if (ProductContainer.listImportedProduct.Count == 0) return;

            /*foreach (MultiSolid ms in ProductContainer.listImportedProduct)
            {
                if (ms.listSolids == null || ms.listSolids.Count == 0) continue;

                foreach (Solid s in ms.listSolids)
                {
                    DrawOneSolid(s);
                }
            }*/
        }

        private static void DrawOneSolid()
        {
            if (verticesList == null) return;


            //Uncomment the following line to see the triangles
            //GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

            /*foreach (Plane plane in solidToDraw.listSolidPlanes)
            {

                GL.Begin(PrimitiveType.Lines);
                GL.Color4(Color.Black);
                GL.LineWidth(1f);
                GL.Vertex3(new Vector3(plane.firstPoint.X, plane.firstPoint.Y, plane.firstPoint.Z));
                GL.Vertex3(new Vector3(plane.secondPoint.X, plane.secondPoint.Y, plane.secondPoint.Z));
                GL.End();

                GL.Begin(PrimitiveType.Lines);
                GL.Color4(Color.Black);
                GL.LineWidth(1f);
                GL.Vertex3(new Vector3(plane.secondPoint.X, plane.secondPoint.Y, plane.secondPoint.Z));
                GL.Vertex3(new Vector3(plane.thirdPoint.X, plane.thirdPoint.Y, plane.thirdPoint.Z));
                GL.End();

                GL.Begin(PrimitiveType.Lines);
                GL.Color4(Color.Black);
                GL.LineWidth(1f);
                GL.Vertex3(new Vector3(plane.thirdPoint.X, plane.thirdPoint.Y, plane.thirdPoint.Z));
                GL.Vertex3(new Vector3(plane.fourthPoint.X, plane.fourthPoint.Y, plane.fourthPoint.Z));
                GL.End();

                GL.Begin(PrimitiveType.Lines);
                GL.Color4(Color.Black);
                GL.LineWidth(1f);
                GL.Vertex3(new Vector3(plane.fourthPoint.X, plane.fourthPoint.Y, plane.fourthPoint.Z));
                GL.Vertex3(new Vector3(plane.firstPoint.X, plane.firstPoint.Y, plane.firstPoint.Z));
                GL.End();

            }*/
            /*GL.Begin(PrimitiveType.Lines);
            GL.Color4(Color.Black);
            GL.LineWidth(1f);
            foreach (System.Numerics.Vector3 vec3 in contourList)
            {

                GL.Vertex3(new Vector3(vec3.X, vec3.Y, vec3.Z));

            }
            GL.End();*/
            GL.EnableVertexAttribArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vertex_buffer_object);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(1);
            GL.BindBuffer(BufferTarget.ArrayBuffer, color_buffer_object);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(2);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vertex_buffer_object);
            GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false,0,0);
            GL.DrawArrays(PrimitiveType.Triangles, 0, verticesList.Count);
            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(2);

        }

        private static void DrawMainCoordinateSystem()
        {
            //Draw Coordynate system
            //Draw axes X
            GL.Begin(PrimitiveType.Lines);
            GL.Color4(Color.Red);
            GL.LineWidth(20f);
            GL.Vertex3(new Vector3(0f, 0f, 0f));
            GL.Vertex3(new Vector3(1f, 0f, 0f));
            GL.End();
            GL.Begin(PrimitiveType.Lines);
            GL.Color4(Color.Green);
            GL.LineWidth(20f);
            GL.Vertex3(new Vector3(0f, 0f, 0f));
            GL.Vertex3(new Vector3(0f, 1f, 0f));
            GL.End();
            GL.Begin(PrimitiveType.Lines);
            GL.Color4(Color.Blue);
            GL.LineWidth(20f);
            GL.Vertex3(new Vector3(0f, 0f, 0f));
            GL.Vertex3(new Vector3(0f, 0f, 1f));
            GL.End();
        }


        public static void Resize(Eto.OpenTK.WinForms.WinGLUserControl RenderPanel, ICamera cam)
        {
            //float aspect_ratio = (float)RenderPanel.ClientSize.Width / (float)RenderPanel.ClientSize.Height;

            Log.Debug($"Screen dimensions are {(float)RenderPanel.ClientSize.Width} : {(float)RenderPanel.ClientSize.Height}");
            cam.GetAspectRatio((float)RenderPanel.ClientSize.Width, (float)RenderPanel.ClientSize.Height);

            //Matrix4 perspective = cam.GetProjectionMatrix((float) RenderPanel.ClientSize.Width, (float) RenderPanel.ClientSize.Height);
            Matrix4 perspective = cam.GetViewMode();

            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref perspective);
            GL.Viewport(0, 0, RenderPanel.Width, RenderPanel.Height);
        }

        public static void RenderStart(Eto.OpenTK.WinForms.WinGLUserControl RenderPanel)
        {
            _vertexBufferObject = GL.GenBuffer();
            _vertexArrayObject = GL.GenVertexArray();
            
            GL.Viewport(0, 0, RenderPanel.Width, RenderPanel.Height);
        }

        public static void GenerateVerticesLists()
        {
            foreach (MultiSolid ms in ProductContainer.listImportedProduct)
            {
                if (ms.solidList == null || ms.solidList.Count == 0) continue;
                var lst = new List<CSG.Sharp.CSG>();
                HoleData holes = new HoleData();
                var holesList = new List<CSG.Sharp.CSG>();
                foreach (CsgSolid csg in ms.solidList)
                {
                    lst.Add(csg.getPolygon());
                    if (holes.holesList.Count > 0)
                    {
                        for (int i = 0; i < holes.holesList.Count; i++)
                        {
                            if (holes.holesList[i].partSurface == csg.partName)
                                holesList.Add(holes.holesList[i].GenerateHole(holes.holesList[i].holeType, csg));
                        }
                    }
                }
                var testx = CsgSolid.GeneratePoly(lst);
                if (holesList.Count > 0)
                {
                    verticesListTest = CsgSolid.GenerateSolidHoles(holesList, testx);
                    contourList = CsgSolid.listTest;
                }
                else
                {
                    verticesListTest = CsgSolid.GenerateSolid(lst);
                    contourList = CsgSolid.listTest;
                }
                var index = 0;
                foreach (System.Numerics.Vector3 vec in verticesListTest)
                {
                    Console.WriteLine(vec);
                    verticesList.Add(vec.X);
                    verticesList.Add(vec.Y);
                    verticesList.Add(vec.Z);
                    if (index <= verticesListTest.Count / 2)
                    {
                        verticesColor.Add(1.0f);
                        verticesColor.Add(0.0f);
                        verticesColor.Add(1.0f);
                    }

                }
            }
            shader = new Shader(vertexShaderCode, fragmentShaderCode);
            shader.Load();
            lightingShader = new Shader(vertexShaderCode, lightingShaderCode);
            lightingShader.Load();
            GL.GenBuffers(1, out vertex_buffer_object);
            GL.GenBuffers(1, out color_buffer_object);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vertex_buffer_object);
            GL.BufferData(BufferTarget.ArrayBuffer, verticesList.ToArray().Length * sizeof(float), verticesList.ToArray(), BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ArrayBuffer, color_buffer_object);
            GL.BufferData(BufferTarget.ArrayBuffer, verticesColor.ToArray().Length * sizeof(float), verticesColor.ToArray(), BufferUsageHint.StaticDraw);

            isLoaded = true;
        }

    }
}
