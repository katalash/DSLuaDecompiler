function implicit_list()
    GlobalList =
    {
        { TRUE, FALSE, TRUE },
        { TRUE, FALSE, TRUE },
        { TRUE, FALSE, TRUE },
        { TRUE, FALSE, TRUE },
        { TRUE, FALSE, TRUE },
        { TRUE, FALSE, TRUE },
        { TRUE, FALSE, TRUE },
    }
end

function explicit_list()
    GlobalList2 =
    {
        [0] = { TRUE, FALSE, TRUE },
        [1] = { TRUE, FALSE, TRUE },
        [2] = { TRUE, FALSE, TRUE },
        [3] = { TRUE, FALSE, TRUE },
        [4] = { TRUE, FALSE, TRUE },
        [5] = { TRUE, FALSE, TRUE },
        [6] = { TRUE, FALSE, TRUE },
    }
end

function zero_start_list()
    GlobalList3 =
    {
        [0] = { TRUE, FALSE, TRUE },
        { TRUE, FALSE, TRUE },
        { TRUE, FALSE, TRUE },
        { TRUE, FALSE, TRUE },
        { TRUE, FALSE, TRUE },
        { TRUE, FALSE, TRUE },
        { TRUE, FALSE, TRUE },
    }
end

function local_key_list()
    local a = 1
    local b = 2
    local c = 3
    GlobalList = { KEY = 5 }
    GlobalList2 =
    {
        [GlobalList.KEY] = { GlobalList.KEY, b, c },
        [a] = { a, b, c },
        [b] = { b, c, a },
        [c] = { c, a, b }
    }
    GlobalList3 =
    {
        a, b, c
    }
end

function long_zero_start_list()
    MagicPutOppositeWeapon = {
        [0] = {TRUE, TRUE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {TRUE, TRUE, TRUE},
        {TRUE, TRUE, TRUE},
        {TRUE, TRUE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {TRUE, TRUE, TRUE},
        {TRUE, TRUE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {TRUE, TRUE, TRUE},
        {FALSE, FALSE, TRUE},
        {TRUE, TRUE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {TRUE, TRUE, TRUE},
        {FALSE, FALSE, TRUE},
        {TRUE, TRUE, TRUE},
        {TRUE, TRUE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {TRUE, TRUE, TRUE},
        {TRUE, TRUE, TRUE},
        {TRUE, TRUE, TRUE},
        {TRUE, TRUE, TRUE},
        {TRUE, TRUE, TRUE},
        {FALSE, FALSE, TRUE},
        {TRUE, TRUE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {TRUE, TRUE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {TRUE, TRUE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {TRUE, TRUE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE},
        {FALSE, FALSE, TRUE}
    }
end