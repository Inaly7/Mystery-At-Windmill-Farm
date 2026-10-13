local NPCData = {
    npcList = {
        {
            id = "Player_001",
            name = "玩家",
            avatarPath = "",
            currentBranchId = 1,
            isFolded = true,
            storyGraphs = {
            }
        }
        ,
        {
            id = "NPC_004",
            name = "描述",
            avatarPath = "",
            currentBranchId = 1,
            isFolded = false,
            storyGraphs = {
                {
                    branchId = 1,
                    storyDescription = "描述需要一个梯子",
                    luaModuleName = "miaosu",
                    luaAssetPath = "Assets/Editor/DialogueData/miaosu.lua"
                }
            }
        }
        ,
        {
            id = "NPC_005",
            name = "大黄",
            avatarPath = "Assets/Res/Sprites/DialoguePortrait/Dog/醉倒.png",
            currentBranchId = 1,
            isFolded = true,
            storyGraphs = {
                {
                    branchId = 1,
                    storyDescription = "全量树（doc 样章，dahuang_01_FROM_DOC）",
                    luaModuleName = "dahuang_01_FROM_DOC",
                    luaAssetPath = "Assets/Editor/DialogueData/FROM_DOC/dahuang_01_FROM_DOC.lua"
                }
            }
        }
        ,
        {
            id = "NPC_488",
            name = "淑芬",
            avatarPath = "Assets/Res/Sprites/DialoguePortrait/Hen/守望.png",
            currentBranchId = 1,
            isFolded = true,
            storyGraphs = {
                {
                    branchId = 1,
                    storyDescription = "树状 FROM_DOC（委托+hub）",
                    luaModuleName = "shufang_01_FROM_DOC",
                    luaAssetPath = "Assets/Editor/DialogueData/FROM_DOC/shufang_01_FROM_DOC.lua"
                }
            }
        }
        ,
        {
            id = "NPC_E03",
            name = "E03_Eavesdrop",
            avatarPath = "",
            currentBranchId = 1,
            isFolded = false,
            storyGraphs = {
                {
                    branchId = 1,
                    storyDescription = "E03 身后偷听",
                    luaModuleName = "xiaojiZTT_e03_FROM_DOC",
                    luaAssetPath = "Assets/Editor/DialogueData/FROM_DOC/xiaojiZTT_e03_FROM_DOC.lua"
                }
            }
        }
        ,
        {
            id = "NPC_009",
            name = "小鸡侦探团",
            avatarPath = "Assets/Res/Sprites/DialoguePortrait/Chicken/装酷.png",
            currentBranchId = 1,
            isFolded = false,
            storyGraphs = {
                {
                    branchId = 1,
                    storyDescription = "树状 FROM_DOC（主交互+hub）",
                    luaModuleName = "xiaojiZTT_01_FROM_DOC",
                    luaAssetPath = "Assets/Editor/DialogueData/FROM_DOC/xiaojiZTT_01_FROM_DOC.lua"
                }
            }
        }
        ,
        {
            id = "NPC_010",
            name = "黑猫",
            avatarPath = "Assets/Res/Sprites/DialoguePortrait/Cat/高傲.png",
            currentBranchId = 1,
            isFolded = true,
            storyGraphs = {
                {
                    branchId = 1,
                    storyDescription = "橡树下+揭穿+NGPlus（树状准稿）",
                    luaModuleName = "heimao_03_FROM_DOC",
                    luaAssetPath = "Assets/Editor/DialogueData/FROM_DOC/heimao_03_FROM_DOC.lua"
                }
            }
        }
        ,
        {
            id = "NPC_011",
            name = "大树",
            avatarPath = "",
            currentBranchId = 1,
            isFolded = true,
            storyGraphs = {
                {
                    branchId = 1,
                    storyDescription = "大橡树 ch1（树状准稿）",
                    luaModuleName = "heimao_tree_01_FROM_DOC",
                    luaAssetPath = "Assets/Editor/DialogueData/FROM_DOC/heimao_tree_01_FROM_DOC.lua"
                }
            }
        }
        ,
        {
            id = "NPC_012",
            name = "悲伤蛙",
            avatarPath = "Assets/Res/Sprites/DialoguePortrait/Frog/丧.png",
            currentBranchId = 1,
            isFolded = false,
            storyGraphs = {
                {
                    branchId = 1,
                    storyDescription = "池塘+薄荷鱼（树状准稿）",
                    luaModuleName = "qingwa_01_FROM_DOC",
                    luaAssetPath = "Assets/Editor/DialogueData/FROM_DOC/qingwa_01_FROM_DOC.lua"
                }
            }
        }
        ,
        {
            id = "NPC_013",
            name = "闪电蜗牛",
            avatarPath = "Assets/Res/Sprites/DialoguePortrait/Snail/待机.png",
            currentBranchId = 1,
            isFolded = false,
            storyGraphs = {
                {
                    branchId = 1,
                    storyDescription = "首次对话",
                    luaModuleName = "wuniu_01_FROM_DOC",
                    luaAssetPath = "Assets/Editor/DialogueData/FROM_DOC/wuniu_01_FROM_DOC.lua"
                }
            }
        }
        ,
        {
            id = "NPC_014",
            name = "乌鸦",
            avatarPath = "Assets/Res/Sprites/DialoguePortrait/WuYa/得意.png",
            currentBranchId = 1,
            isFolded = false,
            storyGraphs = {
                {
                    branchId = 1,
                    storyDescription = "乌鸦全章（树状准稿）",
                    luaModuleName = "wuya_01_FROM_DOC",
                    luaAssetPath = "Assets/Editor/DialogueData/FROM_DOC/wuya_01_FROM_DOC.lua"
                }
            }
        }
        ,
        {
            id = "NPC_015",
            name = "老鼠兄弟",
            avatarPath = "Assets/Res/Sprites/DialoguePortrait/Mouse/兜售.png",
            currentBranchId = 1,
            isFolded = false,
            storyGraphs = {
                {
                    branchId = 1,
                    storyDescription = "墙缝黑市（树状准稿）",
                    luaModuleName = "laoshu_01_FROM_DOC",
                    luaAssetPath = "Assets/Editor/DialogueData/FROM_DOC/laoshu_01_FROM_DOC.lua"
                }
            }
        }
    }
}
return NPCData
