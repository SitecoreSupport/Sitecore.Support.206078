using Sitecore.WFFM.Abstractions.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Sitecore.WFFM.Abstractions.Analytics;
using Sitecore.WFFM.Abstractions.Data;
using Sitecore.WFFM.Abstractions.Shared;
using Sitecore.Form.Core.Utility;
using Sitecore.Form.Core.Pipelines.FormSubmit;
using Sitecore.Form.Core.Data;

namespace Sitecore.Support.Forms.Core.Dependencies
{
  public class DefaultImplActionExecutor : Sitecore.Forms.Core.Dependencies.DefaultImplActionExecutor, IActionExecutor
  {
    private readonly IItemRepository itemRepository;
    private readonly IRequirementsChecker requirementsChecker;
    private readonly ILogger logger;
    private readonly IAnalyticsTracker analyticsTracker;
    public DefaultImplActionExecutor(IItemRepository itemRepository, IRequirementsChecker requirementsChecker, ILogger logger, IResourceManager resourceManager, IAnalyticsTracker analyticsTracker, IWffmDataProvider dataProvider, IFieldProvider fieldProvider, IFormContext formContext) : base(itemRepository, requirementsChecker, logger, resourceManager, analyticsTracker, dataProvider, fieldProvider, formContext)
    {
      this.itemRepository = itemRepository;
      this.requirementsChecker= requirementsChecker;
      this.logger = logger;
      this.analyticsTracker = analyticsTracker;
  }

    void IActionExecutor.ExecuteChecking(Data.ID formID, ControlResult[] fields, IActionDefinition[] actionDefinitions)
    {
      IActionDefinition actionDefinition = null;
      IFormItem formItem = this.itemRepository.CreateFormItem(formID);
      try
      {
        this.RaiseEvent("forms:check", new object[]
        {
      formID,
      fields
        });
        ActionCallContext actionCallContext = new ActionCallContext
        {
          FormItem = formItem
        };
        for (int i = 0; i < actionDefinitions.Length; i++)
        {
          IActionDefinition actionDefinition2 = actionDefinitions[i];
          actionDefinition = actionDefinition2;
          IActionItem actionItem = this.itemRepository.CreateAction(actionDefinition2.ActionID);
          if (actionItem != null)
          {
            ICheckAction checkAction = actionItem.ActionInstance as ICheckAction;
            if (checkAction != null && this.requirementsChecker.CheckRequirements(checkAction.GetType()))
            {
              ReflectionUtils.SetXmlProperties(checkAction, actionDefinition2.Paramaters, true);
              ReflectionUtils.SetXmlProperties(checkAction, actionItem.GlobalParameters, true);
              checkAction.UniqueKey = actionDefinition2.UniqueKey;
              checkAction.ActionID = actionItem.ID;
              checkAction.Execute(formID, fields, actionCallContext);
            }
          }
          else
          {
            this.logger.Warn(string.Format("Web Forms for Marketers : The '{0}' action does not exist", actionDefinition2.ActionID), new object());
          }
        }
      }
      catch (Exception ex)
      {
        if (actionDefinition != null)
        {
          string text = actionDefinition.GetFailureMessage(false, Sitecore.Data.ID.Null);
          if (string.IsNullOrEmpty(text))
          {
            text = ex.Message;
          }
          CheckFailedArgs checkFailedArgs = new CheckFailedArgs(formID, actionDefinition.ActionID, fields, ex)
          {
            ErrorMessage = text
          };
          Sitecore.Pipelines.CorePipeline.Run("errorCheck", checkFailedArgs);
          if (formItem.IsDropoutTrackingEnabled)
          {
            this.analyticsTracker.TriggerEvent(IDs.FormCheckActionErrorId, "Form Check Action Error", formID, checkFailedArgs.ErrorMessage, actionDefinition.GetTitle());
          }
          throw new FormSubmitException(checkFailedArgs.ErrorMessage, actionDefinition.ActionID, ex);
          
        }
        throw;
      }
    }
  }
}