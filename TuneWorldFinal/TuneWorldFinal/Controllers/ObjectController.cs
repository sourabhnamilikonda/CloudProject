using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.AspNet.Identity;
using Amazon;
using System.Security.Principal;
using TuneWorldFinal.Models;
using log4net;
using System.Web.Configuration;

namespace TuneWorldFinal.Controllers
{
    public class ObjectController : ApiController
    {
        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        static IAmazonS3 client;
        //currently set here move these and keys to web.config
        static RegionEndpoint regionep = Amazon.RegionEndpoint.GetBySystemName(WebConfigurationManager.AppSettings["region"]); //Amazon.RegionEndpoint.USEast1;
        static string bucketName = WebConfigurationManager.AppSettings["bucketname"];
        static string keyName = WebConfigurationManager.AppSettings["keyname"];
        static string awskey = WebConfigurationManager.AppSettings["awskey"];
        static string awssecret = WebConfigurationManager.AppSettings["awssecret"];
        static string cloudfrontlink = WebConfigurationManager.AppSettings["cflink"];

        public IEnumerable<Object> Get(string searchterm, string category)
        {
            Log.Debug("GET Request public using searchterm:" + searchterm);
            using (TuneWorldDBEntities entities = new TuneWorldDBEntities())
            {
                entities.Configuration.ProxyCreationEnabled = false;
                switch (category)
                {
                    case "Filename": return entities.Objects.Where(e => (e.ObjectName.Contains(searchterm) || e.ObjectName.Contains(searchterm.ToLower()) || e.ObjectName.Contains(searchterm.ToUpper())) && e.MakePublic.Equals(true)).ToList();
                    case "Songname": return entities.Objects.Where(e => (e.SongName.Contains(searchterm) || e.SongName.Contains(searchterm.ToLower()) || e.SongName.Contains(searchterm.ToUpper())) && e.MakePublic.Equals(true)).ToList();
                    case "Genre": return entities.Objects.Where(e => (e.Genre.Contains(searchterm) || e.Genre.Contains(searchterm.ToLower()) || e.Genre.Contains(searchterm.ToUpper())) && e.MakePublic.Equals(true)).ToList();
                    case "Language": return entities.Objects.Where(e => (e.Language.Contains(searchterm) || e.Language.Contains(searchterm.ToLower()) || e.Language.Contains(searchterm.ToUpper())) && e.MakePublic.Equals(true)).ToList();
                }
                return entities.Objects.Where(e => e.ObjectName.Contains(searchterm) && e.MakePublic.Equals(true)).ToList();
            }
        }

        [Authorize]
        public IEnumerable<Object> Get()
        {
            string userid = User.Identity.GetUserId();

            Log.Debug("GET Request private using userid:" + userid);
            using (TuneWorldDBEntities entities = new TuneWorldDBEntities())
            {
                return entities.Objects.Where(obj => obj.UserId.Equals(userid)).ToList();
            }
        }

        [Authorize]
        public string GetLink(int id)
        {
            Log.Debug("GETLink Request private using objectid:" + id);
            try
            {
                using (TuneWorldDBEntities entities = new TuneWorldDBEntities())
                {
                    var entity = entities.Objects.Where(e => e.ObjectId.Equals(id));

                    if (entity.Count() == 1)
                    {
                        string objectFinal = entity.First().ObjectUploadLink;
                        string urlString = "";

                        using (client = new AmazonS3Client(awskey, awssecret, new AmazonS3Config
                        {
                            RegionEndpoint = regionep,
                            UseAccelerateEndpoint = true
                        }))
                        {
                            urlString = GeneratePreSignedDownloadURL(objectFinal);
                            Log.Debug("GETLink Request urlstring:" + urlString);
                        }
                        return urlString;
                    }
                    return null;
                }
            }
            catch(Exception ex)
            {
                Log.Debug("GETLink Request exception:" + ex.StackTrace);
                return null;
            }
        }

        [Authorize]
        [HttpDelete()]
        public IHttpActionResult Delete(int id)
        {
            Log.Debug("Delete Request entered for id:" + id);
            IHttpActionResult ret = null;
            if (DeleteObject(id))
            {
                ret = Ok(true);
            }
            else
            {
                ret = NotFound();
            }
            return ret;
        }

        [Authorize]
        public async Task<HttpResponseMessage> PostUpload()
        {
            Log.Debug("POST Request entered ");
            // Check if it is multipart/form-data.
            if (!Request.Content.IsMimeMultipartContent())
            {
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
            }

            string userid = User.Identity.GetUserId();
            string username = User.Identity.GetUserName();

            string root = System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data");
            var provider = new MultipartFormDataStreamProvider(root);

            try
            {
                // Read the form data.
                await Request.Content.ReadAsMultipartAsync(provider);

                // Get filename for now only 1 file will be present here
                foreach (MultipartFileData file in provider.FileData)
                {
                    bool bPublic = provider.FormData.GetValues("filepublic")[0] == "true" ? true : false;

                    if (UploadS3(username, file.LocalFileName, file.Headers.ContentDisposition.FileName.Replace("\"", ""), file.Headers.ContentType.MediaType, bPublic))
                    {
                        Log.Debug("POST UploadS3 success ");
                        if (UpdateRDS(User, file, provider))
                        {
                            Log.Debug("POST UpdateRDS success ");
                            return Request.CreateResponse(HttpStatusCode.OK, "hi");
                        }
                        else
                        {
                            //rollback s3 upload here
                        }
                    }
                }
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Not OK");
            }
            catch (System.Exception e)
            {
                Log.Debug("POST exception " + e.StackTrace);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, e);
            }
        }

        //delete related
        private bool DeleteObject(int id)
        {
            Log.Debug("Entered delete object");
            //delete from s3
            using (TuneWorldDBEntities entities = new TuneWorldDBEntities())
            {
                var entity = entities.Objects.Where(e => e.ObjectId.Equals(id));
                if (entity.Count() == 1)
                {
                    using (client = new AmazonS3Client(awskey, awssecret, new AmazonS3Config
                    {
                        RegionEndpoint = regionep,
                        UseAccelerateEndpoint = true
                    }))
                    {
                        try
                        {
                            // Delete the object
                            DeleteObjectRequest request = new DeleteObjectRequest
                            {
                                BucketName = bucketName,
                                Key = entity.First().ObjectUploadLink,
                                //VersionId = versionID
                            };
                            Console.WriteLine("Deleting an object");
                            DeleteObjectResponse resp = client.DeleteObject(request);
                        }
                        catch (AmazonS3Exception s3Exception)
                        {
                            Log.Debug("Delete S3 Exception exception " + s3Exception.Message + s3Exception.StackTrace);
                            Console.WriteLine(s3Exception.Message, s3Exception.InnerException);
                            return false;
                        }

                        //delete from rds
                        try
                        {
                            Log.Debug("Delete S3 success updating RDS ");
                            ObjectModel obj = new ObjectModel(id);
                            string cmdstr = obj.ObjectDeleteDB("Objects", id);
                            DBAccess.DBAccess.InsertUpdateDeleteIntoDB(cmdstr);
                        }
                        catch (Exception e)
                        {
                            Log.Debug("Delete updating RDS excepton" + e.Message + e.StackTrace);
                            return false;
                        }
                        return true;
                    }
                }
                return false;
            }

        }

        //s3 related
        private bool UploadS3(string username, string filepath, string filename, string filetype, bool bPublic)
        {
            try
            {
                client = new AmazonS3Client(awskey, awssecret, new AmazonS3Config
                {
                    RegionEndpoint = regionep,
                    UseAccelerateEndpoint = true
                });

                using (TransferUtility fileTransferUtility = new TransferUtility(client))
                {
                    Console.WriteLine("Uploading an object");
                    if (WritingAnObject(username, filepath, filename, filetype, bPublic))
                        return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Log.Debug("Exception inse uploads3 excepton" + ex.Message + ex.StackTrace);
                throw;
            }
        }

        private bool WritingAnObject(string username, string filepath, string filename, string filetype, bool bPublic)
        {
            try
            {
                PutObjectRequest putRequest1 = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = keyName + "/" + username +"/" + filename,
                    FilePath = filepath,
                    ContentType = filetype
                };
                if(bPublic)
                    putRequest1.CannedACL = S3CannedACL.PublicRead;
                //putRequest1.Metadata.Add("x-amz-meta-title", "someTitle");

                PutObjectResponse response2 = client.PutObject(putRequest1);
                if (response2.HttpStatusCode == HttpStatusCode.OK)
                    return true;
                else
                    return false;
            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                if (amazonS3Exception.ErrorCode != null &&
                    (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId") ||
                        amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
                {
                    Log.Debug("AWS Credentials error.");
                    Log.Debug("Exception  inside WritingAnObject" + amazonS3Exception.Message + amazonS3Exception.StackTrace);
                }
                else
                {
                    Console.WriteLine("Error: '{0}'", amazonS3Exception.Message);
                    Log.Debug("Exception  inside WritingAnObject" + amazonS3Exception.Message + amazonS3Exception.StackTrace);
                }
                return false;
            }
        }

        //rds related
        private bool UpdateRDS(IPrincipal user, MultipartFileData file, MultipartFormDataStreamProvider provider)
        {
            string filename = file.Headers.ContentDisposition.FileName.Replace("\"", "");
            string objectFinal = keyName + "/" + user.Identity.GetUserName() + "/" + filename;
            string uploadtime = DateTime.Now.ToString();
            string userid = user.Identity.GetUserId();
            string objectDesc = provider.FormData.GetValues("filedescription")[0];
            string objectSongname = provider.FormData.GetValues("filesong")[0];
            string objectLanguage = provider.FormData.GetValues("filelanguage")[0];
            string objectGenre = provider.FormData.GetValues("filegenre")[0];
            int iPublic = provider.FormData.GetValues("filepublic")[0] == "true" ? 1 : 0;

            //check if entry already exists based on objectFinal (update or insert)
            bool bInsert = true;
            int objid = 0;
            using (TuneWorldDBEntities entities = new TuneWorldDBEntities())
            {
                var entity = entities.Objects.Where(e => e.UserId.Equals(userid) && e.ObjectUploadLink.Equals(objectFinal));
                if (entity.Count() > 0)
                {
                    bInsert = false;
                    objid = entity.First().ObjectId;
                }
            }

            string urlString = "";
            if (iPublic == 1)
            {
                //here it might not work for other region objects edit later because of region endpoint
                //https://s3.amazonaws.com/tuneworldmain/TuneWorldFolder/bcd%40xyz/Happy+Birthday+Tune.txt
                //urlString = "https://s3.amazonaws.com/" + bucketName + "/" + HttpUtility.UrlEncode(objectFinal);
                string cloudfrontobjectFinal= user.Identity.GetUserName() + "/" + filename;
                urlString =  cloudfrontlink + HttpUtility.UrlEncode(cloudfrontobjectFinal);
            }
            else
            {
                using (client = new AmazonS3Client(awskey, awssecret, new AmazonS3Config
                {
                    RegionEndpoint = regionep,
                    UseAccelerateEndpoint = true
                }))
                {
                    urlString = GeneratePreSignedDownloadURL(objectFinal);
                }
            }
           

            ObjectModel obj = new ObjectModel(userid, filename, objectDesc, objectFinal, urlString, uploadtime, iPublic, objectSongname, objectLanguage, objectGenre);
            string cmdstr = "";
            if (bInsert)
                cmdstr = obj.ObjectInsertDB("Objects");
            else
                cmdstr = obj.ObjjectUpdateDB("Objects", objid);
            DBAccess.DBAccess.InsertUpdateDeleteIntoDB(cmdstr);
            return true;
        }

        //download related
        static string GeneratePreSignedDownloadURL(string objectKey)
        {
            string urlString = "";

            Amazon.CloudFront.AmazonCloudFrontConfig c = new Amazon.CloudFront.AmazonCloudFrontConfig() {
                RegionEndpoint = regionep
            };

            //Amazon.CloudFront.AmazonCloudFrontClient cl = new Amazon.CloudFront.AmazonCloudFrontClient(awskey, awssecret, c);

            GetPreSignedUrlRequest request1 = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                Expires = DateTime.Now.AddMinutes(5)
            };

            try
            {
                urlString = client.GetPreSignedURL(request1);
                //string url = s3Client.GetPreSignedURL(request1);
            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                if (amazonS3Exception.ErrorCode != null &&
                    (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId")
                    ||
                    amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
                {
                    Log.Debug("Check the provided AWS Credentials.");
                }
                else
                {
                    Log.Debug(
                     "Error occurred.  when listing objects Message:" +
                     amazonS3Exception.Message);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Log.Debug(e.Message + e.StackTrace);
            }
            return urlString;
        }
    }
}