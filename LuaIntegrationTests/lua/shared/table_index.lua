-- Simple
function simple()
    local a = 1
    t[a] = 2
end

function simple2()
    local a = 1
    local b = 2
    t[a][b] = 3
end

function simple3()
    local a = 1
    local b = 2
    local c = 3
    t[a][b][c] = 4
end

function function_indices()
    local a = f()
    local b = g()
    local c = h()
    t[a][b][c] = i()
end

function function_indices_args(arg)
    local a = f(arg)
    local b = g(arg)
    local c = h(arg)
    t[a][b][c] = i(arg)
end

function function_indices_self(arg)
    local a = arg:f()
    local b = arg:g()
    local c = arg:h()
    t[a][b][c] = arg:i()
end

function failing(arg, arg2)
    if arg2 == nil then
        return
    end
    local a = arg:f(CONSTANT)
    local b = arg:g()
    local c = arg:h(OTHER_CONSTANT)
    t[a][b][c] = i(arg2)
end 

function index_function_call()
    function get_table()
        return my_table
    end
    return get_table()[5]
end

function index_multiassign_tables()
    function stuff()
        return 1, 2, 3
    end
    function get_table()
        return my_table
    end
    local a
    a, t[1], get_table()[2] = stuff()
end