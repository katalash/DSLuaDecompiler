function multi_call_arg_local(argument)
    local argument, loc = some_call()
    if (loc == 5) then
        some_call()
        return
    end
    return 0
end