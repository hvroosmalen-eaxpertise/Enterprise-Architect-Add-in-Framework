﻿
using System;
using WT=WorkTrackingFramework;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using System.Linq;
using Newtonsoft.Json.Linq;
using EAAddinFramework.WorkTracking;


namespace EAAddinFramework.WorkTracking.TFS
{
	/// <summary>
	/// Description of WorkItem.
	/// </summary>
	public class TFSWorkItem:WorkItem
	{
		
		public TFSWorkItem(Project ownerProject, int workitemID, ListofWorkItemsResponse.Fields fields):base(ownerProject)
		{
			this.ID = workitemID.ToString();
			this.type = fields.SystemWorkItemType;
			this.title = fields.SystemTitle;
			this.state = fields.SystemState;
			this.description = fields.SystemDescription;
			this.assignedTo = fields.SystemAssignedTo;
			this.area = fields.SystemAreaPath;
			this.iteration = fields.SystemIterationPath;
		}
		public override string iteration 
		{
			get 
			{
				return this.ownerProject.name + @"\" + base.iteration;
			}
			set 
			{
				base.iteration = value.Replace(this.ownerProject.name,string.Empty);
			}
		}
		public override string area 
		{
			get 
			{
				return this.ownerProject.name + @"\" + base.area;
			}
			set 
			{
				base.area = value.Replace(this.ownerProject.name,string.Empty);
			}
		}

	}
}