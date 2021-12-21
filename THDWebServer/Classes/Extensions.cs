using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Blazored.Modal;
using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.ProtectedBrowserStorage;
using Radzen;
using THDWebServer.Pages.Modals;
using ThunderED;

namespace THDWebServer.Classes
{
    public static class Extensions
    {
        public static List<T> ApplyAjaxFilters<T>(this List<T> list, LoadDataArgs args, out int count)
        {
            return ApplyAjaxFilters(list.AsEnumerable(), args, out count);
        }

        public static List<T> ApplyAjaxFilters<T>(this IEnumerable<T> list, LoadDataArgs args, out int count)
        {
            var query = list.AsQueryable();
            if (!string.IsNullOrEmpty(args.Filter))
                query = query.Where(args.Filter);
            count = query.Count();

            if (!string.IsNullOrEmpty(args.OrderBy))
                query = query.OrderBy(args.OrderBy);
            if (args.Skip.HasValue)
                query = query.Skip(args.Skip.Value);
            if (args.Top.HasValue)
                query = query.Take(args.Top.Value);
            return query.ToList();
        }

        public static List<T> ApplyAjaxFilters<T>(this IQueryable<T> query, LoadDataArgs args, out int count)
        {
            count = query.Count();

            if (!string.IsNullOrEmpty(args.Filter))
                query = query.Where(args.Filter);

            if (!string.IsNullOrEmpty(args.OrderBy))
                query = query.OrderBy(args.OrderBy);
            if (args.Skip.HasValue)
                query = query.Skip(args.Skip.Value);
            if (args.Top.HasValue)
                query = query.Take(args.Top.Value);
            return query.ToList();
        }

        public static async Task<T> GetAndClear<T>(this ProtectedSessionStorage storage, string name)
        {
            if (string.IsNullOrEmpty(name)) return default;
            var value = await storage.GetAsync<T>(name);
            await storage.DeleteAsync(name);
            return value;
        }

        public static async Task SafeSet(this ProtectedSessionStorage storage, string name, object value)
        {
            if(string.IsNullOrEmpty(name)) return;
            await storage.SetAsync(name, value);
        }

        public static async Task SafeSet(this ProtectedSessionStorage storage, KeyValuePair<string, object> pair)
        {
            if (string.IsNullOrEmpty(pair.Key)) return;
            await storage.SetAsync(pair.Key, pair.Value);
        }

        public static async Task<bool> ShowConfirm(this IModalService modal, string header = null, string message = null)
        {
            var options = new ModalOptions() { HideCloseButton = true, Class = "blazored-modal2" };
            var parameters = new ModalParameters();
            parameters.Add("Message", message);
            return !(await modal.Show<Confirm>(header ?? LM.Get("webConfirmation"), parameters, options).Result).Cancelled;
        }

        public static async Task ShowError(this IModalService modal, string header = null, string message = null)
        {
            var options = new ModalOptions() { HideCloseButton = true, Class= "blazored-modal2" };
            var parameters = new ModalParameters();
            parameters.Add("Message", message ?? LM.Get("webGenericErrorMessage"));
            await modal.Show<ErrorDialog>(header ?? LM.Get("webConfirmation"), parameters, options).Result;
        }

        public static async Task ShowModal<T>(this IModalService modal, object entry, string header = null, string paramName = null, Dictionary<string, object> prms = null)
            where T: IComponent
        {
            var options = new ModalOptions() { HideCloseButton = false, DisableBackgroundCancel = true, Class = "blazored-modal2", ContentScrollable = true };
            var parameters = new ModalParameters();
            parameters.Add(paramName ?? "Entry", entry);      
            if(prms != null)
                foreach(var (key,value) in prms)
                    parameters.Add(key, value);

            await modal.Show<T>(header ?? LM.Get("webConfirmation"), parameters, options).Result;
        }
    }
}
