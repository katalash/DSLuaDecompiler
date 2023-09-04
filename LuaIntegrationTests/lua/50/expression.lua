function self_arg(input)
    input:func(CONST)
end

function simple_and(arg1, arg2)
    return arg1 and arg2
end

function simple_and_local(arg1, arg2)
    local result = arg1 and arg2
    return result
end

function simple_or(arg1, arg2)
    return arg1 or arg2
end

function simple_or_local(arg1, arg2)
    local result = arg1 or arg2
    return result
end

function conditional_assignment(arg1, arg2, arg3)
    return arg1 and arg2 or arg3
end

function conditional_assignment_local(arg1, arg2, arg3)
    local result = arg1 and arg2 or arg3
    return result
end

function test_type(arg)
    local ret = arg
    if type(arg) == "nil" then
        ret = "nil"
    end
    if type(arg) == "boolean" then
        ret = arg and "true" or "false"
    end
    return ret
end