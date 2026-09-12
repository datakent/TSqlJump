using System;
using System.IO;
using System.Text;

namespace TSqlJump
{
    public static class CodeTemplateInitializer
    {
        private const string DefaultTomlBase64 =
            "W1tzbmlwcGV0XV0NCm5hbWUgPSAiU2VsZWN0IFF1ZXJ5Ig0Kc2hvcnROYW1lID0gInNmIg0Kc2hvcnRjdXQgPSAiRjEyIg0KdHNxbCA9ICIiIg0KU0VMRUNUICogRlJPTSAiIiINCg0KW1tzbmlwcGV0XV0NCm5hbWUgPSAiU2VsZWN0IFRvcCAxIFF1ZXJ5Ig0Kc2hvcnROYW1lID0gInNmMSINCnNob3J0Y3V0ID0gIiINCnRzcWwgPSAiIiINClNFTEVDVCBUT1AoMSkgKiBGUk9NICIiIg0KDQpbW3NuaXBwZXRdXQ0KbmFtZSA9ICJTZWxlY3QgV2hlcmUgUXVlcnkiDQpzaG9ydE5hbWUgPSAic3ciDQpzaG9ydGN1dCA9ICJGMTEiDQp0c3FsID0gIiIiDQpTRUxFQ1QgKiBGUk9NIA0KV0hFUkUgIiIiDQoNCltbc25pcHBldF1dDQpuYW1lID0gIlNlbGVjdCBUb3AgMSBXaGVyZSBRdWVyeSINCnNob3J0TmFtZSA9ICJzdzEiDQpzaG9ydGN1dCA9ICIiDQp0c3FsID0gIiIiDQpTRUxFQ1QgVE9QKDEpICogRlJPTSANCldIRVJFICIiIg0KDQpbW3NuaXBwZXRdXQ0KbmFtZSA9ICJEZWxldGUgRnJvbSINCnNob3J0TmFtZSA9ICJkZiINCnNob3J0Y3V0ID0gIkYxMCINCnRzcWwgPSAiIiINCkRFTEVURSBGUk9NICIiIg0KDQpbW3NuaXBwZXRdXQ0KbmFtZSA9ICJEZWxldGUgV2hlcmUiDQpzaG9ydE5hbWUgPSAiZHciDQpzaG9ydGN1dCA9ICJGOSINCnRzcWwgPSAiIiINCkRFTEVURSBGUk9NIA0KV0hFUkUgIiIiDQoNCltbc25pcHBldF1dDQpuYW1lID0gIlVwZGF0ZSINCnNob3J0TmFtZSA9ICJ1cyINCnNob3J0Y3V0ID0gIiINCnRzcWwgPSAiIiINClVQREFURSANClNFVCANCldIRVJFICIiIg0KDQpbW3NuaXBwZXRdXQ0KbmFtZSA9ICJDdXJzb3IiDQpzaG9ydE5hbWUgPSAiY3IiDQpzaG9ydGN1dCA9ICIiDQp0c3FsID0gIiIiDQpERUNMQVJFIEMxIENVUlNPUiBGT1JXQVJEX09OTFkgUkVBRF9PTkxZIEZPUg0KCVNFTEVDVCAqIEZST00gDQpPUEVOIEMxOw0KDQpGRVRDSCBORVhUIEZST00gQzEgSU5UTyANCg0KV0hJTEUgQEBGRVRDSF9TVEFUVVMgPSAwDQpCRUdJTg0KCQ0KCUZFVENIIE5FWFQgRlJPTSBDMSBJTlRPIA0KRU5EDQoNCkNMT1NFIEMxOw0KREVBTExPQ0FURSBDMTsNCiIiIg0K";

        public static void EnsureDefaultFiles()
        {
            CodeTemplatePaths.EnsureDirectory();

            if (File.Exists(CodeTemplatePaths.SnippetsFile))
                return;

            byte[] data =
                Convert.FromBase64String(DefaultTomlBase64);

            string content =
                Encoding.UTF8.GetString(data);

            File.WriteAllText(
                CodeTemplatePaths.SnippetsFile,
                content,
                new UTF8Encoding(false));
        }
    }
}