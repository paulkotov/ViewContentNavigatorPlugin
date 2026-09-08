using System;
using System.Linq;
using System.Web.Script.Serialization;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using ViewContentNavigator.Models;

namespace ViewContentNavigator.Services
{
    public sealed class ExtensibleStorageSettingsStore : IDocumentSettingsStore
    {
        private static readonly Guid SchemaGuid = new Guid("C7D361DC-B207-46E2-AC66-650A21A84A20");
        private const string SchemaName = "ViewContentNavigatorSettings";
        private const string FieldName = "Json";
        private const string VendorId = "MYVCN";

        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public NavigatorSettings Load(Document doc)
        {
            try
            {
                var schema = Schema.Lookup(SchemaGuid);
                if (schema == null)
                    return null;

                var storage = FindStorage(doc, schema);
                if (storage == null)
                    return null;

                var entity = storage.GetEntity(schema);
                if (entity == null || !entity.IsValid())
                    return null;

                var json = entity.Get<string>(FieldName);
                return string.IsNullOrEmpty(json) ? null : _serializer.Deserialize<NavigatorSettings>(json);
            }
            catch
            {
                return null;
            }
        }

        public void Save(Document doc, NavigatorSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var json = _serializer.Serialize(settings);

            using (var tx = new Transaction(doc, "Навигатор: сохранение настроек"))
            {
                tx.Start();

                var schema = GetOrCreateSchema();
                var storage = FindStorage(doc, schema) ?? DataStorage.Create(doc);

                var entity = new Entity(schema);
                entity.Set(FieldName, json);
                storage.SetEntity(entity);

                tx.Commit();
            }
        }

        private static Schema GetOrCreateSchema()
        {
            var existing = Schema.Lookup(SchemaGuid);
            if (existing != null)
                return existing;

            var builder = new SchemaBuilder(SchemaGuid);
            builder.SetSchemaName(SchemaName);
            builder.SetVendorId(VendorId);
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField(FieldName, typeof(string));
            return builder.Finish();
        }

        private static DataStorage FindStorage(Document doc, Schema schema)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(DataStorage))
                .Cast<DataStorage>()
                .FirstOrDefault(ds =>
                {
                    var entity = ds.GetEntity(schema);
                    return entity != null && entity.IsValid();
                });
        }
    }
}
