using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Services;
using System.IO;
using System.Collections;

using SuperMap.Data;
using SuperMap.Mapping;
using SuperMap.Data.Conversion;
using System.Diagnostics;
using SuperMap.Analyst.SpatialAnalyst;

namespace WebService
{
    /// <summary>
    /// Service1 的摘要说明
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    
    public class Service1 : System.Web.Services.WebService
    {
       
        #region 变量区
        private Datasource fileDatasource;
        private DatasetVector sourceDatasetVector;
        private SuperMap.Data.Workspace fileWorkspace;
        private DatasetVector m_bufferDataset;
        double mu = 0;
        double pfm = 0;
        #endregion

        #region webservice 测试 方法HelloWorld
        [WebMethod]
        public string HelloWorld()
        {
            return "Hello World";
        }
        #endregion

        #region webservice 接收文件服务端处理
        [WebMethod(Description = "TransFile")]
        public string TransFile(byte[] fileBt, double width, string fileName, bool ifCreate)
        {
            // 0长度文件返回 0
            string rst = "0";
            if (fileBt.Length == 0){
                return rst;
            }
            string filePath = Server.MapPath(System.Configuration.ConfigurationManager.AppSettings["CSVPath"].ToString());   //存储文件路径
            //创建系统日期文件夹,避免同一文件夹下文件太多问题，避免同一地图应文档重名
             filePath = filePath + DateTime.Now.ToString("yyyyMMdd") + "\\";

            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }

            FileStream fstream;
            //是否创建新文件
            if (ifCreate)
            {
                fstream = new FileStream(filePath + fileName, FileMode.Create);
            }
            else
            {
                fstream = new FileStream(filePath + fileName, FileMode.Append);
            }
            try
            {
                fstream.Write(fileBt, 0, fileBt.Length);   //二进制转换成文件
                //上传成功返回 1
                fstream.Close();
            }
            catch (Exception ex)
            {
                //上传失败返回 1
                rst = "error";
            }
            finally
            {
                fstream.Close();
            }
            //读取坐标文件转换面积
            //设置为实际值
            string imgPath = filePath + fileName;
            //坐标点导入处理
            StreamReader objReader = new StreamReader(imgPath);
            string sLine = "";
            ArrayList LineList = new ArrayList();
            while (sLine != null)
            {
                sLine = objReader.ReadLine();
                if (sLine != null && !sLine.Equals(""))
                {
                    LineList.Add(sLine);
                }
            }
            objReader.Close();
            bool isrewrite = true;
            string sourcePath = Server.MapPath(System.Configuration.ConfigurationManager.AppSettings["sourcePath"].ToString());   //存储文件路径
            string targetPath = Server.MapPath(System.Configuration.ConfigurationManager.AppSettings["targetPath"].ToString());   //存储文件路径            String targetPath = str + "\\tempFolder\\temp_gkfqd.shp";
            string temp = GenerateRandomCode(10);
            targetPath = targetPath + DateTime.Now.ToString("yyyyMMdd") + "\\";

            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }
            targetPath = targetPath + temp + "\\";
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }
            System.IO.File.Copy(sourcePath + "testG.smwu", targetPath + "testG.smwu", isrewrite);
            System.IO.File.Copy(sourcePath + "test.udd", targetPath + "test.udd", isrewrite);
            System.IO.File.Copy(sourcePath + "test.udb", targetPath + "test.udb", isrewrite);
            this.fileWorkspace = new SuperMap.Data.Workspace();
            string workSpaceFilePath = Server.MapPath(System.Configuration.ConfigurationManager.AppSettings["BinPath"].ToString());   //存储文件路径          
            try
            {
                //打开工作空间及地图文件类型
                WorkspaceConnectionInfo conInfo = new WorkspaceConnectionInfo(targetPath + "testG.smwu");
                fileWorkspace.Open(conInfo);
                fileDatasource = fileWorkspace.Datasources["test"];
                sourceDatasetVector = fileDatasource.Datasets["dataT"] as DatasetVector;
                Recordset  recordset = (sourceDatasetVector as DatasetVector).GetRecordset(false, CursorType.Dynamic);
                // 获得记录集对应的批量更新对象
                Recordset.BatchEditor editor = recordset.Batch;
                // 开始批量添加，将 example 数据集每条记录对应的几何对象添加到数据集中
                editor.Begin();
                //删除所有记录
                recordset.DeleteAll();
                Point2Ds points = new Point2Ds();
                for (int i = 1; i < LineList.Count - 1; i++)
                {
                    string[] fieldInfoListZ = LineList[i].ToString().Split(',');
                    Point2D point2D = new Point2D();
                    point2D.X = double.Parse(fieldInfoListZ[0].ToString());
                    point2D.Y = double.Parse(fieldInfoListZ[1].ToString());
                    points.Add(point2D);
                }
                GeoLine geolineE = new GeoLine();
                geolineE.AddPart(points);
                recordset.AddNew(geolineE);
                editor.Update();
                //调用创建矢量数据集缓冲区方法
                //设置缓冲区分析参数
                BufferAnalystParameter bufferAnalystParam = new BufferAnalystParameter();

                //FLAT:平头缓冲。ROUND:圆头缓冲。
                bufferAnalystParam.EndType = BufferEndType.Flat;
                //左侧缓冲距离，单位：米。
                bufferAnalystParam.LeftDistance = width/2;
                //右侧缓冲距离，单位：米。
                bufferAnalystParam.RightDistance = width/2;
                String bufferName = "bufferRegionDt";
                bufferName = fileDatasource.Datasets.GetAvailableDatasetName(bufferName);
                m_bufferDataset = fileDatasource.Datasets.Create(new DatasetVectorInfo(bufferName, DatasetType.Region));
                //设置投影信息 不设置 报投影不一致错误
                m_bufferDataset.Datasource.Datasets["bufferRegionDt"].PrjCoordSys = (fileDatasource.Datasets["dataT"] as DatasetVector).PrjCoordSys;
                Boolean isTrue = SuperMap.Analyst.SpatialAnalyst.BufferAnalyst.CreateBuffer(recordset, m_bufferDataset, bufferAnalystParam, false, true);
                Recordset recordsetS = m_bufferDataset.Query("SmID=1", CursorType.Static);
                pfm = double.Parse(recordsetS.GetFieldValue("SMAREA").ToString());
                mu = pfm * 0.0015;
                double gongqing = 0.0666667 * mu;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            } 
            rst = String.Format("{0:F2}", mu);
            fileWorkspace.Close();
            return rst;
        }
        #endregion

        #region 取不重复名字
        public static string GenerateRandomCode(int length)
          {
             var result = new System.Text.StringBuilder();
             for (var i = 0; i < length; i++)
            {
                 var r = new Random(Guid.NewGuid().GetHashCode());
                 result.Append(r.Next(0, 10));
             }
             return result.ToString();
          }
        #endregion


    }
}