using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace TuneWorldFinal.Models
{
    public class ObjectModel
    {
        //added this model for now remove and use one from EF
        public int ObjectId { get; set; }
        public string UserId { get; set; }
        public string ObjectName { get; set; }
        public string ObjectDesc { get; set; }
        public string ObjectUploadLink { get; set; }
        public string ObjectDownloadLink { get; set; }
        public string ObjectUpdateTime { get; set; }
        public int MakePublic { get; set; }
        public string ObjectSongname { get; set; }
        public string ObjectLanguage { get; set; }
        public string ObjectGenre { get; set; }

        public ObjectModel(int objId)
        {
            ObjectId = objId;
        }

        public ObjectModel(string userid, string objectname, string objectdesc, string objectuploadlink, string objectdownloadlink,
             string objectupdatetime, int makepublic, string objectsongname, string objectlanguage, string objectgenre)
        {
            UserId = userid; 
            ObjectName = objectname;
            ObjectDesc = objectdesc;
            ObjectUploadLink = objectuploadlink;
            ObjectDownloadLink = objectdownloadlink;
            ObjectUpdateTime = objectupdatetime;
            MakePublic = makepublic;
            ObjectSongname = objectsongname;
            ObjectLanguage = objectlanguage;
            ObjectGenre = objectgenre;
        }

        public string ObjectInsertDB(string tablename)
        {
            return "insert into " + tablename + " values ('" + this.UserId + "','" + this.ObjectName + "','" + this.ObjectDesc + "','" + this.ObjectUploadLink + "','" + this.ObjectDownloadLink + "','" + this.ObjectUpdateTime + "'," + this.MakePublic + ",'" + this.ObjectSongname + "','" + this.ObjectLanguage + "','" + this.ObjectGenre + "')";
        }

        public string ObjjectUpdateDB(string tablename, int objId)
        {
            return "update " + tablename + " set ObjectDesc='" + this.ObjectDesc + "', ObjectDownloadLink = '" + this.ObjectDownloadLink + "', ObjectUpdateTime = '" + this.ObjectUpdateTime + "',MakePublic =" + this.MakePublic + ", SongName = '" + this.ObjectSongname + "', Language = '" + this.ObjectLanguage + "', Genre='" + this.ObjectGenre + "' where ObjectId = " + objId;
        }

        public string ObjectDeleteDB(string tablename, int objId)
        {
            return "delete from " + tablename + " where ObjectId = " + objId;
        }
    }
}