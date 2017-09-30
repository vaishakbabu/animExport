using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using Autodesk.Maya.OpenMaya;
using Autodesk.Maya.OpenMayaAnim;

[assembly: MPxCommandClass(typeof(MayaNetPlugin.animExport),"animExport")]

namespace MayaNetPlugin
{
    public class animExport : MPxCommand, IMPxCommand
    {

        /// <summary>
        /// Gets the animfile path, creates an XML document and starts writing.
        /// </summary>
        /// <param name="args"></param>
        public override void doIt(MArgList args)
        {
            //get the filepath from the arguments.
            string filePath;
            if (args.length == 0)
            {
                filePath = @"C:\Temp\anim.xml";
            }
            else
            {
                filePath = args.asString(0);
                if (!File.Exists(filePath))
                {
                    var myFile = File.Create(filePath);
                    myFile.Close();
                }
                else
                {
                    File.Delete(filePath);
                    var myFile = File.Create(filePath);
                    myFile.Close();
                }
            }

            //XML Settings
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "    ",
                NewLineChars = "\r\n",
                NewLineHandling = NewLineHandling.Replace
            };

            //create an XML doc
            XmlWriter xmlWriter = XmlWriter.Create(filePath, settings);

            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement("Data");

            //get current selection list
            MSelectionList currentSel = new MSelectionList();
            MGlobal.getActiveSelectionList(currentSel);

            //Iterate through each selection and set the animation
            MItSelectionList iter = new MItSelectionList(currentSel);
            MDagPath dagPath = new MDagPath();

            //for each object
            for (; !iter.isDone; iter.next())
            {
                iter.getDagPath(dagPath);

                xmlWriter.WriteStartElement("object");
                xmlWriter.WriteAttributeString("id", dagPath.fullPathName);

                //write to XML
                writeAnim(dagPath, xmlWriter);

                xmlWriter.WriteEndElement(); //end object

            }

            xmlWriter.WriteEndDocument();
            xmlWriter.Close();
            
        }


        /// <summary>
        /// This function write the animation data to the XML file.
        /// </summary>
        /// <param name="dagPath"></param>
        /// <param name="xmlWriter"></param>
        static void writeAnim(MDagPath dagPath, XmlWriter xmlWriter)
        {

            //export static attributes
            exportStaticAttributes(dagPath, xmlWriter);

            MPlugArray animatedPlugs = new MPlugArray();

            //if animated, export the keyed attributes.
            if (MAnimUtil.isAnimated(dagPath))
            {
                //find the animatedPlugs
                MAnimUtil.findAnimatedPlugs(dagPath, animatedPlugs);

                foreach (MPlug plug in animatedPlugs)
                {
                    //foreach of the values get the animCurve and all the values.

                    string attribute = plug.name.ToString();
                    //get the attribute name
                    attribute = attribute.Split('.')[1];

                    xmlWriter.WriteStartElement(attribute);
                    xmlWriter.WriteAttributeString("type", "keyed");

                    //get the animCurve from the plug
                    MFnAnimCurve animCurve = new MFnAnimCurve(plug);

                    //get the keys values and all other details

                    //preInfinity, postInfinity and weighted
                    MFnAnimCurve.InfinityType preI = animCurve.preInfinityType;
                    MFnAnimCurve.InfinityType posI = animCurve.postInfinityType;
                    bool isWeighted = animCurve.isWeighted;

                    int preInfinity = infinityToInt(preI);
                    int postInfinity = infinityToInt(posI);

                    xmlWriter.WriteStartElement("infinity");
                    xmlWriter.WriteAttributeString("preInfinity", preInfinity.ToString());
                    xmlWriter.WriteAttributeString("postInfinity", postInfinity.ToString());
                    xmlWriter.WriteAttributeString("weightedTangents", isWeighted.ToString());
                    xmlWriter.WriteEndElement();    //end of infinity

                    //keys

                    for (uint i = 0; i < animCurve.numKeys; i++)
                    {
                        double time = animCurve.time(i).value;

                        double value = 0;
                        if (attribute.Contains("rotate"))
                            value = animCurve.value(i) * (180 / Math.PI);
                        else
                            value = animCurve.value(i);

                        int breakdown = Convert.ToInt16(animCurve.isBreakdown(i));
                        bool tanLock = animCurve.tangentsLocked(i);
                        bool weightLock = animCurve.weightsLocked(i);

                        MFnAnimCurve.TangentType iTT = new MFnAnimCurve.TangentType();
                        MFnAnimCurve.TangentType oTT = new MFnAnimCurve.TangentType();

                        iTT = animCurve.inTangentType(i);
                        oTT = animCurve.outTangentType(i);

                        double inWeight = 0.0;
                        double outWeight = 0.0;
                        MAngle inAngle = new MAngle();
                        MAngle outAngle = new MAngle();

                        animCurve.getTangent(i, inAngle, ref inWeight, true);
                        animCurve.getTangent(i, outAngle, ref outWeight, false);

                        xmlWriter.WriteStartElement("key");
                        xmlWriter.WriteAttributeString("breakdown", breakdown.ToString());
                        xmlWriter.WriteAttributeString("inAngle", inAngle.asDegrees.ToString());
                        xmlWriter.WriteAttributeString("inTangentType", tangentTypeToString(iTT));
                        xmlWriter.WriteAttributeString("inWeight", inWeight.ToString());
                        xmlWriter.WriteAttributeString("key", time.ToString());
                        xmlWriter.WriteAttributeString("lock", tanLock.ToString());
                        xmlWriter.WriteAttributeString("outAngle", outAngle.asDegrees.ToString());
                        xmlWriter.WriteAttributeString("outTangentType", tangentTypeToString(oTT));
                        xmlWriter.WriteAttributeString("outWeight", outWeight.ToString());
                        xmlWriter.WriteAttributeString("value", value.ToString());
                        xmlWriter.WriteAttributeString("weightLock", weightLock.ToString());
                        xmlWriter.WriteEndElement();  //end of key

                    }

                    xmlWriter.WriteEndElement();  //end of keyed attribute
                }
            }

        }


        /// <summary>
        /// Converts Infinity Type to corresponding int value.
        /// </summary>
        /// <param name="infinity"></param>
        /// <returns></returns>
        static int infinityToInt(MFnAnimCurve.InfinityType infinity)
        {
            if (infinity == MFnAnimCurve.InfinityType.kConstant)
            {
                return 0;
            }
            else if (infinity == MFnAnimCurve.InfinityType.kLinear)
            {
                return 1;
            }
            else if (infinity == MFnAnimCurve.InfinityType.kCycle)
            {
                return 3;
            }
            else if (infinity == MFnAnimCurve.InfinityType.kCycleRelative)
            {
                return 4;
            }
            else
            {
                return 5;
            }
        }


        static string tangentTypeToString(MFnAnimCurve.TangentType tt)
        {

            if (tt == MFnAnimCurve.TangentType.kTangentAuto)
            {
                return "auto";
            }
            else if (tt == MFnAnimCurve.TangentType.kTangentFixed)
            {
                return "fixed";
            }
            else if (tt == MFnAnimCurve.TangentType.kTangentGlobal)
            {
                return "global";
            }
            else if (tt == MFnAnimCurve.TangentType.kTangentLinear)
            {
                return "linear";
            }
            else if (tt == MFnAnimCurve.TangentType.kTangentFlat)
            {
                return "flat";
            }
            else if (tt == MFnAnimCurve.TangentType.kTangentSmooth)
            {
                return "smooth";
            }
            else if (tt == MFnAnimCurve.TangentType.kTangentStep)
            {
                return "step";
            }
            else if (tt == MFnAnimCurve.TangentType.kTangentClamped)
            {
                return "clamped";
            }
            else if (tt == MFnAnimCurve.TangentType.kTangentPlateau)
            {
                return "plateau";
            }
            else if (tt == MFnAnimCurve.TangentType.kTangentStepNext)
            {
                return "stepnext";
            }
            else
            {
                return "auto";
            }
        }

        /// <summary>
        /// Export the static attributes to the XML file.
        /// </summary>
        /// <param name="dagPath"></param>
        /// <param name="xmlWriter"></param>
        static void exportStaticAttributes(MDagPath dagPath, XmlWriter xmlWriter)
        {
            MFnDependencyNode depNode = new MFnDependencyNode(dagPath.node);
            uint count = depNode.attributeCount;

            for (uint i = 0; i < count; i++)
            {
                MObject attr = depNode.attribute(i);
                MPlug plug = depNode.findPlug(attr);

                //if attribute is connected, skip

                if (plug.isConnected)
                    continue;

                try
                {
                    if(plug.isKeyable)
                    {
                        string attribute = plug.name.Split('.')[1];

                        //get the value and add it to xml
                        MFn.Type api_type = plug.attribute.apiType;
                        if (api_type == MFn.Type.kNumericAttribute)
                        {

                            MFnNumericAttribute num_type = new MFnNumericAttribute(plug.attribute);
                            MFnNumericData.Type type = num_type.unitType;

                            if (type == MFnNumericData.Type.kBoolean)
                            {
                                bool bool_val = plug.asBool();
                                xmlWriter.WriteStartElement(attribute);
                                xmlWriter.WriteAttributeString("type", "static");
                                xmlWriter.WriteAttributeString("value", bool_val.ToString());
                                xmlWriter.WriteEndElement(); //end object
                            }
                            else if (type == MFnNumericData.Type.kDouble)
                            {
                                double d_val = plug.asDouble();
                                xmlWriter.WriteStartElement(attribute);
                                xmlWriter.WriteAttributeString("type", "static");
                                xmlWriter.WriteAttributeString("value", d_val.ToString());
                                xmlWriter.WriteEndElement(); //end object
                            }
                            else if (type == MFnNumericData.Type.kLong)
                            {
                                long l_val = plug.asInt();
                                xmlWriter.WriteStartElement(attribute);
                                xmlWriter.WriteAttributeString("type", "static");
                                xmlWriter.WriteAttributeString("value", l_val.ToString());
                                xmlWriter.WriteEndElement(); //end object
                            }

                        }
                        else if (api_type == MFn.Type.kDoubleLinearAttribute)
                        {
                            double d_val = plug.asDouble();
                            if (d_val != 0)
                            {
                                xmlWriter.WriteStartElement(attribute);
                                xmlWriter.WriteAttributeString("type", "static");
                                xmlWriter.WriteAttributeString("value", d_val.ToString());
                                xmlWriter.WriteEndElement(); //end object
                            }
                        }
                        else if (api_type == MFn.Type.kDoubleAngleAttribute)
                        {
                            double d_val = plug.asDouble();
                            d_val *= 180 / Math.PI; //convert to degree

                            if (d_val != 0)
                            {
                                xmlWriter.WriteStartElement(attribute);
                                xmlWriter.WriteAttributeString("type", "static");
                                xmlWriter.WriteAttributeString("value", d_val.ToString());
                                xmlWriter.WriteEndElement(); //end object
                            }
                        }
                        else if (api_type == MFn.Type.kEnumAttribute)
                        {
                            int val = plug.asInt();
                            xmlWriter.WriteStartElement(attribute);
                            xmlWriter.WriteAttributeString("type", "static");
                            xmlWriter.WriteAttributeString("value", val.ToString());
                            xmlWriter.WriteEndElement(); //end object
                            
                        }                       

                    }
                }
                catch (Exception e)
                {
                    MGlobal.displayWarning(e.Message);
                }
            }

        }
    }
    
}
